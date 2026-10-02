using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Poink.Sim.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Simulation;
using Poink.Sim.Trader;
using Poink.Sim.Trader.Commission;
using Poink.Sim.Trader.Risk;

namespace Poink.Sim.Bots;

/// <summary>
/// A flat bot opens long (else short) when that side's conditions all match; a bot in a position closes when
/// the close conditions match. Orders go through the account's normal path, so risk policies still gate them.
/// </summary>
internal sealed class BotService : IBotService
{
	private readonly ITraderFactory _traderFactory;
	private readonly ITickRepository _tickRepository;
	private readonly ILogger<BotService> _logger;
	private readonly object _lock = new();
	private readonly TxFoldBarsCache _txFoldBars = new();
	private readonly BotReentryGuard _reentryGuard = new();

	private IReadOnlyList<Bot> _bots = Array.Empty<Bot>();
	private RobotOptions? _options;
	private (int Count, DateTime LastOpen, int Window, int Offset, int Pips, int Cols, double PipSize, bool Grid)? _barDataKey;
	private RobotBarData? _barData;
	private (RobotBarData BarData, int AtrPeriod)? _levelsKey;
	private RobotMarketLevels _levels;
	private DateTime? _lastEvaluation;
	private SymbolTradingLimits? _limits;

	public BotService(ITraderFactory traderFactory, ITickRepository tickRepository, ILogger<BotService> logger)
	{
		_traderFactory = traderFactory;
		_tickRepository = tickRepository;
		_logger = logger;
	}

	public IReadOnlyList<Bot> Bots
	{
		get
		{
			lock (_lock)
			{
				return _bots;
			}
		}
	}

	public IReadOnlyList<Bot> CreateBots(
		BotAccountOptions account,
		IReadOnlyList<string>? riskPolicyNames,
		RiskPolicyToggles? riskToggles
	)
	{
		var settings = new TraderSettings(
			account.Leverage,
			account.StopOutLevelPercent,
			new PercentOfNotionalCommission(account.CommissionRatePercent),
			riskPolicyNames,
			riskToggles
		);
		var bots = Enumerable
			.Range(1, Math.Max(1, account.BotCount))
			.Select(i => new Bot
			{
				Id = i,
				Label = $"Bot {i}",
				Account = _traderFactory.Create(account.StartingCash, settings),
			})
			.ToList();
		lock (_lock)
		{
			_bots = bots;
			_limits = account.Limits;
			_lastEvaluation = null;
			_reentryGuard.Reset();
		}
		_logger.LogInformation(
			"Created {Count} bots: startingCash={StartingCash}, leverage={Leverage}, commission={Commission}%, stopOut={StopOut}%",
			bots.Count,
			account.StartingCash,
			account.Leverage,
			account.CommissionRatePercent,
			account.StopOutLevelPercent
		);
		return bots;
	}

	public void SetOptions(RobotOptions options)
	{
		lock (_lock)
		{
			_options = options;
		}
		_logger.LogInformation(
			"Bot options updated: enabled={Enabled}, openLongKeys={OpenLong}, openShortKeys={OpenShort}, closeLongKeys={CloseLong}, closeShortKeys={CloseShort}, cooldown={Cooldown}s, requireRearm={RequireRearm}",
			options.Enabled,
			options.OpenLong.EnabledKeys.Count,
			options.OpenShort.EnabledKeys.Count,
			options.CloseLong.EnabledKeys.Count,
			options.CloseShort.EnabledKeys.Count,
			options.CooldownSeconds,
			options.RequireRearm
		);
	}

	public IReadOnlyList<BotAction> OnTick(BotTickContext tick)
	{
		RobotOptions? options;
		IReadOnlyList<Bot> bots;
		lock (_lock)
		{
			options = _options;
			bots = _bots;
		}
		if (options is not { Enabled: true } || tick.RecentBars.Count == 0)
			return Array.Empty<BotAction>();

		ManagePositions(bots, tick, options);
		if (!options.HasRules || !IsEvaluationDue(tick.Timestamp, options.EvaluationIntervalSeconds))
			return Array.Empty<BotAction>();

		bool anyFlat = bots.Any(b => b.Account.CurrentPosition is null && b.Account.CurrentPendingOrder is null);
		bool anyOpen = bots.Any(b => b.Account.CurrentPosition is not null);
		if (!anyFlat && !anyOpen)
			return Array.Empty<BotAction>();

		if (options.UsesGroup(RuleSignalGroup.TxFold))
			_ = RefreshTxFoldAsync(tick, options);

		var barData = BarDataFor(tick, options);
		if (barData is null)
			return Array.Empty<BotAction>();
		var market = new RuleSignalMarket(
			barData.Bars,
			barData.Window,
			tick.Bid,
			tick.Ask,
			tick.TypicalSpread,
			tick.Timestamp,
			tick.BarSize,
			options.Signals
		);
		var context = new RobotRuleContext(barData, tick.Price, _txFoldBars.Bars, market);
		var levels = LevelsFor(barData, options.Signals.AtrPeriod);

		var actions = new List<BotAction>();
		foreach (var bot in bots)
		{
			_reentryGuard.Observe(bot);
			if (bot.Account.CurrentPosition is { } position)
			{
				var closeRules = options.CloseRulesFor(position.Direction == PositionDirection.Long);
				if (context.Matches(closeRules, position) && Close(bot, tick) is { } closed)
					actions.Add(closed);
			}
			else if (bot.Account.CurrentPendingOrder is null && TryEnter(bot, options, context, levels, tick) is { } entry)
			{
				actions.Add(entry);
			}
		}
		return actions;
	}

	// Long is tried before short, as before; the guard only ever narrows which entries are allowed.
	private BotAction? TryEnter(Bot bot, RobotOptions options, RobotRuleContext context, RobotMarketLevels levels, BotTickContext tick)
	{
		if (_reentryGuard.IsCoolingDown(bot, tick.Timestamp, options.CooldownSeconds))
			return null;
		if (options.RequireRearm)
			_reentryGuard.Rearm(bot, context.Matches(options.OpenLong), context.Matches(options.OpenShort));

		if (
			_reentryGuard.IsArmed(bot, isLong: true, options.RequireRearm)
			&& context.Matches(options.OpenLong)
			&& Enter(bot, options, levels, isLong: true, tick) is { } longEntry
		)
			return longEntry;
		if (
			_reentryGuard.IsArmed(bot, isLong: false, options.RequireRearm)
			&& context.Matches(options.OpenShort)
			&& Enter(bot, options, levels, isLong: false, tick) is { } shortEntry
		)
			return shortEntry;
		return null;
	}

	// Time is the simulated tick clock, so the cadence is the same at any playback speed; a clock that
	// jumps backwards (seek or reset) counts as due.
	private bool IsEvaluationDue(DateTime timestamp, double intervalSeconds)
	{
		if (_lastEvaluation is { } last && timestamp >= last && (timestamp - last).TotalSeconds < intervalSeconds)
			return false;
		_lastEvaluation = timestamp;
		return true;
	}

	// The bar-derived inputs only change when a bar completes, so they are rebuilt then rather than on every tick.
	private RobotBarData? BarDataFor(BotTickContext tick, RobotOptions options)
	{
		var key = (
			tick.RecentBars.Count,
			tick.RecentBars[^1].OpenTime,
			options.BarWindow,
			options.BarOffset,
			options.XGridPips,
			options.XGridCols,
			tick.PipSize,
			options.UsesGroup(RuleSignalGroup.XGrid)
		);
		if (_barDataKey == key)
			return _barData;

		_barData = RobotBarData.Create(tick.RecentBars, tick.RecentHistograms, tick.PipSize, options, key.Item8);
		_barDataKey = key;
		return _barData;
	}

	private BotAction? Enter(Bot bot, RobotOptions options, RobotMarketLevels levels, bool isLong, BotTickContext tick)
	{
		var capital = bot.Account.Snapshot(tick.Bid, tick.Ask);
		double entryPrice = isLong ? tick.Ask : tick.Bid;
		if (
			RobotPositionHelper.PlanEntry(
				options.Position,
				options.PositionRules,
				levels,
				isLong,
				entryPrice,
				capital.Cash,
				bot.Account.Leverage,
				_limits!,
				SizeMultiplier(bot, capital.Equity, options.PositionRules)
			)
			is not { } plan
		)
			return null;

		var decision = new SimulationDecision(
			isLong ? SimulationAction.EnterLong : SimulationAction.EnterShort,
			plan.CapitalFraction,
			plan.StopLossPrice,
			plan.TakeProfitPrice,
			Reason: "robot"
		);
		return Apply(bot, decision, tick, isLong ? "opened a long" : "opened a short");
	}

	private static double SizeMultiplier(Bot bot, double equity, RobotPositionRules rules)
	{
		double peak = bot.Account.MaxEquity;
		bool inDrawdown = rules.DrawdownThrottleEnabled && peak > 0 && (peak - equity) / peak * 100 >= rules.DrawdownPercent;
		return inDrawdown ? rules.DrawdownSizeMultiplier : 1;
	}

	// Runs every tick, not only when rules are evaluated, so a trailing stop follows price as closely as the replay allows.
	private void ManagePositions(IReadOnlyList<Bot> bots, BotTickContext tick, RobotOptions options)
	{
		var rules = options.PositionRules;
		if ((!rules.BreakevenEnabled && !rules.TrailEnabled) || _limits is null)
			return;

		double? atr = null;
		bool atrResolved = false;
		foreach (var bot in bots)
		{
			if (bot.Account.CurrentPosition is not { } position)
				continue;
			if (rules.TrailEnabled && !atrResolved)
			{
				atr = BarDataFor(tick, options) is { } barData ? LevelsFor(barData, options.Signals.AtrPeriod).Atr : null;
				atrResolved = true;
			}
			if (RobotPositionHelper.ManagedStopPrice(position, tick.Bid, tick.Ask, rules, atr, _limits.PriceStep) is { } stop)
				bot.Account.ModifyProtection(stop, position.TakeProfitPrice);
		}
	}

	// ATR and the value-area levels only change when the bar data does, so they are cached against it.
	private RobotMarketLevels LevelsFor(RobotBarData barData, int atrPeriod)
	{
		var key = (barData, atrPeriod);
		if (_levelsKey == key)
			return _levels;

		var atrSeries = MarketRegimeHelper.AtrSeries(barData.Bars, atrPeriod);
		var stats = RangeStatsHelper.Compute(barData.Samples, w => w.Price, w => (double)w.Count);
		_levels = new RobotMarketLevels(atrSeries.Count > 0 ? atrSeries[^1] : null, stats?.Poc, stats?.Vah, stats?.Val);
		_levelsKey = key;
		return _levels;
	}

	private BotAction? Close(Bot bot, BotTickContext tick) =>
		Apply(bot, new SimulationDecision(SimulationAction.Close, Reason: "robot"), tick, "closed the position");

	private BotAction? Apply(Bot bot, SimulationDecision decision, BotTickContext tick, string description)
	{
		if (bot.Account.Apply(decision, tick.Bid, tick.Ask, tick.Timestamp) is not { } execution)
			return null;
		_logger.LogInformation("{Bot} {Description}", bot.Label, description);
		return new BotAction(bot, execution, description);
	}

	private async Task RefreshTxFoldAsync(BotTickContext tick, RobotOptions options)
	{
		try
		{
			await _txFoldBars.RefreshIfNeededAsync(
				_tickRepository,
				tick.Table,
				tick.RecentBars[^1].CloseTime.AddTicks(1),
				tick.RangeStart,
				options.TxFoldSeconds
			);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Bots failed to load TxFold bars for table {Table}", tick.Table);
		}
	}
}
