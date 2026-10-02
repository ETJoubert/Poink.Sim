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

	private IReadOnlyList<Bot> _bots = Array.Empty<Bot>();
	private RobotOptions? _options;
	private (int Count, DateTime LastOpen, int Window, int Offset, int Pips, int Cols, double PipSize, bool Grid)? _barDataKey;
	private RobotBarData? _barData;
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
			"Bot options updated: enabled={Enabled}, openLongKeys={OpenLong}, openShortKeys={OpenShort}, closeKeys={Close}",
			options.Enabled,
			options.OpenLong.EnabledKeys.Count,
			options.OpenShort.EnabledKeys.Count,
			options.Close.EnabledKeys.Count
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
		if (options is not { Enabled: true, HasRules: true } || tick.RecentBars.Count == 0)
			return Array.Empty<BotAction>();

		if (!IsEvaluationDue(tick.Timestamp, options.EvaluationIntervalSeconds))
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
		var context = new RobotRuleContext(barData, tick.Price, _txFoldBars.Bars);

		var actions = new List<BotAction>();
		foreach (var bot in bots)
		{
			if (bot.Account.CurrentPosition is not null)
			{
				if (context.Matches(options.Close) && Close(bot, tick) is { } closed)
					actions.Add(closed);
			}
			else if (bot.Account.CurrentPendingOrder is null)
			{
				if (context.Matches(options.OpenLong) && Enter(bot, options, isLong: true, tick) is { } longEntry)
					actions.Add(longEntry);
				else if (context.Matches(options.OpenShort) && Enter(bot, options, isLong: false, tick) is { } shortEntry)
					actions.Add(shortEntry);
			}
		}
		return actions;
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

	private BotAction? Enter(Bot bot, RobotOptions options, bool isLong, BotTickContext tick)
	{
		var capital = bot.Account.Snapshot(tick.Bid, tick.Ask);
		double entryPrice = isLong ? tick.Ask : tick.Bid;
		if (
			RobotPositionHelper.PlanEntry(options.Position, isLong, entryPrice, capital.Cash, bot.Account.Leverage, _limits!)
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
