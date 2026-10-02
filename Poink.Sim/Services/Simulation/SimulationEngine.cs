using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Poink.Sim.Bots;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Trader;
using Poink.Sim.Trader.Risk;

namespace Poink.Sim.Simulation;

/// <summary>
/// Interactive replay driver for one shared, in-process simulation run: plays, pauses and
/// steps ticks through the shared aggregator and trader, and publishes an immutable
/// snapshot after each callback for the UI to render.
/// </summary>
internal sealed class SimulationEngine : ISimulationEngine
{
	private const int CallbackIntervalMs = 100;
	private const int BaseUnitsPerCallback = 5;
	// headroom above 5 * MaxFiniteMultiplier
	private const int FiniteSpeedUnitsCap = 100_000;
	private const double MaxFiniteMultiplier = 10_000;

	// time budget per callback while uncapped, not a tick cap
	private const int UncappedSliceMs = 100;

	private const int MaxRecentBars = 300;
	private const int MaxRecentTicks = 5000;
	private const int MaxRecentLog = 200;

	// One bucket per pip, so the XGrid's pip-multiple rows always cover whole buckets -- a coarser bucket would leave rows
	// narrower than it empty between populated ones.
	private static double PriceBucketWidthFor(SimulationConfig config) => config.PipSize;

	private readonly object _lock = new();
	private bool _isWarmingUp;
	private readonly Timer _timer;

	private sealed class TraderSession
	{
		public required int Id;
		public required string Label;
		public required Trader.ITrader Trader;
		public readonly RingBuffer<SimulationLogEntry> RecentLog = new(MaxRecentLog);
		public readonly List<TradeEquityPoint> TradeEquity = new();
		public int RecordedTradeCount;
		public double? OpenTradeMaxEquity;
		public double? OpenTradeMinEquity;
	}

	private Func<IEnumerator<Tick>>? _openReader;
	private IEnumerator<Tick>? _reader;
	private int _cursor;
	private int _totalCount;
	private int _warmupCount;
	private SimulationConfig? _config;
	private SimulationBarAggregator? _aggregator;
	private List<TraderSession> _traderSessions = new();
	private double _speedMultiplier = 1.0;
	private bool _uncapped;
	private bool _isPlaying;


	private RiskPolicyToggles? _riskToggles;

	private readonly RingBuffer<HlocAvdtBar> _recentBars = new(MaxRecentBars);
	private readonly RingBuffer<BarPriceHistogram> _recentBarHistograms = new(MaxRecentBars);
	private readonly RingBuffer<Tick> _recentTicks = new(MaxRecentTicks);

	private double? _lastPrice;
	private double? _lastBid;
	private double? _lastAsk;
	private DateTime? _lastTimestamp;

	private readonly ITickRepository _tickRepository;
	private readonly IBotService _botService;
	private readonly IRiskPolicyService _riskPolicyService;
	private readonly ILogger<SimulationEngine> _logger;

	public SimulationEngine(
		ITickRepository tickRepository,
		IBotService botService,
		IRiskPolicyService riskPolicyService,
		ILogger<SimulationEngine> logger
	)
	{
		_tickRepository = tickRepository;
		_botService = botService;
		_riskPolicyService = riskPolicyService;
		_logger = logger;
		_timer = new Timer(TimerCallback, null, Timeout.Infinite, Timeout.Infinite);
	}

	public SimulationSnapshot? Snapshot { get; private set; }
	public bool IsLoading { get; private set; }
	public string? Error { get; private set; }

	public SimulationProgress? Progress { get; private set; }

	public event Action? OnChanged;
	public event Action? OnProgress;

	public bool TryLoad(SimulationConfig config)
	{
		lock (_lock)
		{
			if (IsLoading)
				return false;
			IsLoading = true;
			Error = null;
		}
		OnChanged?.Invoke();

		_ = Task.Run(() =>
		{
			try
			{
				long count = _tickRepository.CountTicks(config.Table, config.LoadFrom, config.To);
				long warmupCount =
					config.Warmup > TimeSpan.Zero
						? _tickRepository.CountTicks(config.Table, config.LoadFrom, config.From)
						: 0;
				if (count - warmupCount <= 0)
					throw new InvalidOperationException("no ticks in the selected range");

				IEnumerator<Tick> OpenReader() =>
					_tickRepository.ReadTicks(config.Table, config.LoadFrom, config.To).GetEnumerator();
				FinishLoad(config, (int)Math.Min(count, int.MaxValue), (int)warmupCount, OpenReader, "historic");
			}
			catch (Exception ex)
			{
				lock (_lock)
				{
					IsLoading = false;
					Error = ex.Message;
				}
				_logger.LogError(ex, "Failed to load simulation for table {Table}", config.Table);
			}
			PublishSnapshot();
		});

		return true;
	}

	private void FinishLoad(
		SimulationConfig config,
		int totalCount,
		int warmupCount,
		Func<IEnumerator<Tick>> openReader,
		string source
	)
	{
		lock (_lock)
		{
			_timer.Change(Timeout.Infinite, Timeout.Infinite);
			_reader?.Dispose();
			_openReader = openReader;
			_reader = openReader();
			_cursor = 0;
			_totalCount = totalCount;
			_warmupCount = warmupCount;
			_config = config;
			var previousPolicyStates = _riskToggles?.States();
			_riskToggles = _riskPolicyService.CreateToggles(config.RiskPolicyNames);
			if (previousPolicyStates is not null)
				_riskToggles?.RestoreFrom(previousPolicyStates);
			_traderSessions = CreateSessions(config);
			_aggregator = config.Aggregate
				? new SimulationBarAggregator(config.BarSize, PriceBucketWidthFor(config))
				: null;
			_recentBars.Clear();
			_recentBarHistograms.Clear();
			_recentTicks.Clear();
			_lastPrice = null;
			_lastBid = null;
			_lastAsk = null;
			_lastTimestamp = null;
			WarmUpLocked();
			_isPlaying = false;
			IsLoading = false;
		}

		_logger.LogInformation(
			"Loaded {Source} simulation: table={Table}, from={From}, to={To}, ticks={TotalCount}",
			source,
			config.Table,
			config.From,
			config.To,
			totalCount
		);
	}

	private List<TraderSession> CreateSessions(SimulationConfig config) =>
		_botService
			.CreateBots(AccountOptionsFor(config), config.RiskPolicyNames, _riskToggles)
			.Select(bot => new TraderSession
			{
				Id = bot.Id,
				Label = bot.Label,
				Trader = bot.Account,
			})
			.ToList();

	private static BotAccountOptions AccountOptionsFor(SimulationConfig config) =>
		new(
			config.StartingCash,
			config.TraderCount,
			config.Leverage,
			config.CommissionRatePercent,
			config.StopOutLevelPercent,
			config.PipSize,
			config.Limits
		);

	public bool Play()
	{
		lock (_lock)
		{
			if (_reader is null || _isPlaying || _cursor >= _totalCount)
				return false;
			_isPlaying = true;
		}
		_timer.Change(0, Timeout.Infinite);
		PublishSnapshot();
		_logger.LogInformation("Simulation playback started");
		return true;
	}

	public bool Pause()
	{
		int cursor,
			total;
		lock (_lock)
		{
			if (!_isPlaying)
				return false;
			_isPlaying = false;
			cursor = _cursor;
			total = _totalCount;
		}
		PublishSnapshot();
		_logger.LogInformation("Simulation playback paused at {Cursor}/{Total}", cursor, total);
		return true;
	}

	public bool Step()
	{
		int cursor,
			total;
		lock (_lock)
		{
			if (_isPlaying || _reader is null || _cursor >= _totalCount)
				return false;
			AdvanceOneUnitLocked();
			cursor = _cursor;
			total = _totalCount;
		}
		PublishSnapshot();
		_logger.LogInformation("Simulation stepped to {Cursor}/{Total}", cursor, total);
		return true;
	}

	public void SetSpeed(double multiplier)
	{
		lock (_lock)
		{
			_uncapped = false;
			_speedMultiplier = Math.Clamp(multiplier, 0.1, MaxFiniteMultiplier);
		}
		PublishSnapshot();
	}

	public void SetMaxSpeed()
	{
		lock (_lock)
		{
			_uncapped = true;
		}
		PublishSnapshot();
	}

	public bool SetAggregation(bool aggregate, TimeSpan barSize)
	{
		lock (_lock)
		{
			if (_config is null)
				return false;
			if (_config.Aggregate == aggregate && _config.BarSize == barSize)
				return false;

			_config = _config with { Aggregate = aggregate, BarSize = barSize };
			_recentBars.Clear();
			_recentBarHistograms.Clear();

			if (aggregate)
			{
				var aggregator = new SimulationBarAggregator(barSize, PriceBucketWidthFor(_config));
				foreach (var tick in _recentTicks)
				{
					var completed = aggregator.Feed(tick);
					if (completed is not null)
						AppendBar(completed.Value.Bar, completed.Value.Histogram);
				}
				_aggregator = aggregator;
			}
			else
			{
				_aggregator = null;
			}
		}
		PublishSnapshot();
		_logger.LogInformation(
			"Simulation aggregation changed: aggregate={Aggregate}, barSize={BarSize}",
			aggregate,
			barSize
		);
		return true;
	}

	public bool Reset()
	{
		lock (_lock)
		{
			if (_config is null || _openReader is null)
				return false;
			ResetToStartLocked();
		}
		PublishSnapshot();
		_logger.LogInformation("Simulation reset to start of range");
		return true;
	}

	private void ResetToStartLocked()
	{
		_timer.Change(Timeout.Infinite, Timeout.Infinite);
		_reader?.Dispose();
		_reader = _openReader!();
		_cursor = 0;
		_traderSessions = CreateSessions(_config);
		_aggregator = _config.Aggregate
			? new SimulationBarAggregator(_config.BarSize, PriceBucketWidthFor(_config))
			: null;
		_recentBars.Clear();
		_recentBarHistograms.Clear();
		_recentTicks.Clear();
		_lastPrice = null;
		_lastBid = null;
		_lastAsk = null;
		_lastTimestamp = null;
		WarmUpLocked();
		_isPlaying = false;
	}

	private void WarmUpLocked()
	{
		if (_warmupCount == 0)
			return;

		_isWarmingUp = true;
		while (_cursor < _warmupCount)
			AdvanceOneUnitLocked();
		_isWarmingUp = false;

		_traderSessions = CreateSessions(_config);
	}

	// Trader state is only valid built up tick by tick, so a backward seek replays from
	// the start; either way every tick to the target still runs, so a large jump takes a while.
	public bool Seek(int targetIndex)
	{
		int cursor,
			total;
		lock (_lock)
		{
			if (_config is null || _openReader is null)
				return false;

			int clamped = Math.Clamp(targetIndex + _warmupCount, _warmupCount, _totalCount);
			if (clamped < _cursor)
				ResetToStartLocked();
			else
			{
				_timer.Change(Timeout.Infinite, Timeout.Infinite);
				_isPlaying = false;
			}

			while (_cursor < clamped)
				AdvanceOneUnitLocked();

			cursor = _cursor;
			total = _totalCount;
		}
		PublishSnapshot();
		_logger.LogInformation("Simulation sought to {Cursor}/{Total}", cursor, total);
		return true;
	}

	// Back to the setup screen.
	public void Unload()
	{
		lock (_lock)
		{
			_timer.Change(Timeout.Infinite, Timeout.Infinite);
			_reader?.Dispose();
			_reader = null;
			_openReader = null;
			_cursor = 0;
			_totalCount = 0;
			_warmupCount = 0;
			_config = null;
			_riskToggles = null;
			_traderSessions = new();
			_aggregator = null;
			_isPlaying = false;
			_recentBars.Clear();
			_recentBarHistograms.Clear();
			_recentTicks.Clear();
			_lastPrice = null;
			_lastBid = null;
			_lastAsk = null;
			_lastTimestamp = null;
		}
		Snapshot = null;
		Progress = null;
		Error = null;
		OnChanged?.Invoke();
		OnProgress?.Invoke();
		_logger.LogInformation("Simulation unloaded");
	}

	private void TimerCallback(object? state)
	{
		bool reschedule;
		bool wasUncapped;
		lock (_lock)
		{
			wasUncapped = _uncapped;
			if (!_isPlaying || _reader is null)
			{
				reschedule = false;
			}
			else if (_uncapped)
			{
				var sw = Stopwatch.StartNew();
				while (_cursor < _totalCount && sw.ElapsedMilliseconds < UncappedSliceMs)
					AdvanceOneUnitLocked();

				if (_cursor >= _totalCount)
					_isPlaying = false;
				reschedule = _isPlaying;
			}
			else
			{
				int units = Math.Clamp(
					(int)Math.Round(BaseUnitsPerCallback * _speedMultiplier),
					1,
					FiniteSpeedUnitsCap
				);
				for (int i = 0; i < units && _cursor < _totalCount; i++)
					AdvanceOneUnitLocked();

				if (_cursor >= _totalCount)
					_isPlaying = false;
				reschedule = _isPlaying;
			}
		}

		if (wasUncapped && reschedule)
			PublishProgress();
		else
			PublishSnapshot();

		if (reschedule)
			_timer.Change(wasUncapped ? 0 : CallbackIntervalMs, Timeout.Infinite);
	}

	private void AdvanceOneUnitLocked()
	{
		if (!_reader!.MoveNext())
		{
			_cursor = _totalCount;
			return;
		}
		var tick = _reader.Current;
		_cursor++;

		double price = (tick.Bid + tick.Ask) / 2.0;
		_lastPrice = price;
		_lastBid = tick.Bid;
		_lastAsk = tick.Ask;
		_lastTimestamp = tick.Timestamp;

		AppendTick(tick);

		foreach (var session in _traderSessions)
		{
			var maintenance = session.Trader.Advance(tick.Bid, tick.Ask, tick.Timestamp);
			foreach (var fill in maintenance.AutomaticFills)
				AppendLog(session, LogEntryFor(fill, price, tick.Timestamp));
			TrackTradeEquityLocked(session, tick.Bid, tick.Ask, tick.Timestamp);
		}

		if (_aggregator is not null)
		{
			var completed = _aggregator.Feed(tick);
			if (completed is not null)
				AppendBar(completed.Value.Bar, completed.Value.Histogram);

			RunBotsLocked(tick, price);
		}
	}

	// Equity is only sampled while a position is open (and once more when a trade closes), so a long flat
	// stretch costs nothing and every closed trade keeps its own high/low regardless of run length.
	private static void TrackTradeEquityLocked(TraderSession session, double bid, double ask, DateTime timestamp)
	{
		var trader = session.Trader;
		bool tradeClosed = trader.TradeLog.Count > session.RecordedTradeCount;
		if (trader.CurrentPosition is null && !tradeClosed)
			return;

		double equity = trader.Snapshot(bid, ask).Equity;
		session.OpenTradeMaxEquity = Math.Max(session.OpenTradeMaxEquity ?? equity, equity);
		session.OpenTradeMinEquity = Math.Min(session.OpenTradeMinEquity ?? equity, equity);
		if (!tradeClosed)
			return;

		session.TradeEquity.Add(
			new TradeEquityPoint(
				timestamp,
				equity,
				session.OpenTradeMaxEquity.Value,
				session.OpenTradeMinEquity.Value,
				trader.TradeLog.Count
			)
		);
		session.RecordedTradeCount = trader.TradeLog.Count;
		session.OpenTradeMaxEquity = null;
		session.OpenTradeMinEquity = null;
	}

	public bool SetRiskPolicyEnabled(string policyName, bool enabled)
	{
		bool changed;
		lock (_lock)
		{
			changed =
				_traderSessions.FirstOrDefault()?.Trader is IRiskInspectable insp
				&& insp.SetPolicyEnabled(policyName, enabled);
		}

		if (!changed)
			return false;
		PublishSnapshot();
		_logger.LogInformation("Risk policy {Policy} {State}", policyName, enabled ? "enabled" : "disabled");
		return true;
	}

	public bool MoveRiskPolicy(string policyName, int delta)
	{
		bool changed;
		lock (_lock)
		{
			changed =
				_traderSessions.FirstOrDefault()?.Trader is IRiskInspectable insp && insp.MovePolicy(policyName, delta);
		}

		if (!changed)
			return false;
		PublishSnapshot();
		return true;
	}

	public bool SetRiskPolicyParameter(string policyName, string key, double value)
	{
		bool changed;
		lock (_lock)
		{
			changed =
				_traderSessions.FirstOrDefault()?.Trader is IRiskInspectable insp
				&& insp.SetPolicyParameter(policyName, key, value);
		}

		if (!changed)
			return false;
		PublishSnapshot();
		_logger.LogInformation("Risk policy {Policy} parameter {Key} set to {Value}", policyName, key, value);
		return true;
	}

	// Gives every bot a fresh account built from the new options; the replay position is untouched, so any
	// open positions and trade history of the previous accounts are discarded.
	public bool ApplyAccountOptions(BotAccountOptions options)
	{
		lock (_lock)
		{
			if (_config is null)
				return false;

			_config = _config with
			{
				StartingCash = options.StartingCash,
				TraderCount = options.BotCount,
				Leverage = options.Leverage,
				CommissionRatePercent = options.CommissionRatePercent,
				StopOutLevelPercent = options.StopOutLevelPercent,
				PipSize = options.PipSize,
				Limits = options.Limits,
			};
			_traderSessions = CreateSessions(_config);
		}
		PublishSnapshot();
		_logger.LogInformation("Bot account options applied to the running simulation");
		return true;
	}

	public void SetRobotOptions(RobotOptions options) => _botService.SetOptions(options);

	// Every tick of the run feeds the bots; nothing trades during warm-up, so replayed history never opens positions.
	private void RunBotsLocked(Tick tick, double price)
	{
		if (_isWarmingUp || _config is null)
			return;

		var actions = _botService.OnTick(
			new BotTickContext(
				tick.Timestamp,
				price,
				tick.Bid,
				tick.Ask,
				_recentBars,
				_recentBarHistograms,
				_config.Table,
				_config.LoadFrom,
				_config.PipSize
			)
		);
		foreach (var action in actions)
		{
			var session = _traderSessions.First(s => s.Id == action.Bot.Id);
			AppendLog(session, LogEntryFor(action.Execution, price, tick.Timestamp));
			TrackTradeEquityLocked(session, tick.Bid, tick.Ask, tick.Timestamp);
		}
	}

	public bool SubmitManualDecision(int traderId, SimulationDecision decision, out RiskPolicyDenial? denial)
	{
		denial = null;
		lock (_lock)
		{
			if (_config is null)
				return false;
			var session = _traderSessions.FirstOrDefault(s => s.Id == traderId);
			if (session is null)
				return false;

			double bid = _lastBid ?? 0,
				ask = _lastAsk ?? 0;
			var timestamp = _lastTimestamp ?? DateTime.UtcNow;
			var execution = session.Trader.Apply(decision, bid, ask, timestamp);
			TrackTradeEquityLocked(session, bid, ask, timestamp);
			if (execution is { } exec)
			{
				AppendLog(session, LogEntryFor(exec, (bid + ask) / 2.0, timestamp));
				denial = session.Trader is IRiskInspectable insp ? insp.LastDenial : null;
			}
		}
		PublishSnapshot();
		return true;
	}

	private void AppendBar(HlocAvdtBar bar, BarPriceHistogram histogram)
	{
		_recentBars.Add(bar);
		_recentBarHistograms.Add(histogram);
	}

	private void AppendTick(Tick tick) => _recentTicks.Add(tick);

	private void AppendLog(TraderSession session, SimulationLogEntry entry) => session.RecentLog.Add(entry);

	private static SimulationLogEntry LogEntryFor(TradeExecution execution, double price, DateTime timestamp) =>
		new(
			timestamp,
			execution.Blocked ? $"{execution.Action} (blocked)" : execution.Action.ToString(),
			price,
			execution.Direction,
			execution.Size,
			execution.RealizedPnl,
			execution.Commission,
			execution.Reason
		);

	private void PublishSnapshot()
	{
		SimulationSnapshot? snapshot;
		lock (_lock)
		{
			var bars = _recentBars.ToList();
			var barHistograms = _recentBarHistograms.ToList();
			if (_aggregator?.CurrentPartialBar is { } partial)
			{
				bars.Add(partial);
				barHistograms.Add(_aggregator.CurrentPartialHistogram!);
			}

			var primaryRisk = _traderSessions.FirstOrDefault()?.Trader as IRiskInspectable;

			snapshot =
				_config is null || _reader is null
					? null
					: new SimulationSnapshot(
						_config,
						_isPlaying,
						_cursor >= _totalCount,
						_speedMultiplier,
						_uncapped,
						_cursor - _warmupCount,
						_totalCount - _warmupCount,
						_lastTimestamp,
						_lastPrice,
						_lastBid,
						_lastAsk,
						_traderSessions
							.Select(ts => new TraderSnapshot(
								ts.Id,
								ts.Label,
								ts.Trader.Snapshot(_lastBid ?? 0, _lastAsk ?? 0),
								ts.Trader.CurrentPosition,
								ts.Trader.CurrentPendingOrder,
								ts.Trader.TradeLog.Count,
								ts.RecentLog.ToList(),
								ts.Trader.TradeLog.ToList(),
								ts.Trader.MaxEquity,
								ts.Trader.MaxDrawdown,
								ts.Trader.Leverage,
								ts.Trader.StopOutLevelPercent,
								ts.Trader is IRiskInspectable inspectable
									? inspectable.PolicyStatuses()
									: Array.Empty<RiskPolicyStatus>(),
								ts.Trader is IRiskInspectable denialInspectable ? denialInspectable.LastDenial : null,
								ts.TradeEquity.ToList()
							))
							.ToList(),
						bars,
						barHistograms,
						_recentTicks.ToList(),
						primaryRisk?.PolicyStates() ?? Array.Empty<RiskPolicySnapshot>()
					);
		}
		Snapshot = snapshot;
		Progress = snapshot is null
			? null
			: new SimulationProgress(
				snapshot.CursorIndex,
				snapshot.TotalCount,
				snapshot.CurrentTimestampUtc,
				snapshot.Config.From,
				snapshot.Config.To
			);
		OnChanged?.Invoke();
		OnProgress?.Invoke();
	}

	private void PublishProgress()
	{
		lock (_lock)
		{
			if (_config is null)
				return;
			Progress = new SimulationProgress(
				_cursor - _warmupCount,
				_totalCount - _warmupCount,
				_lastTimestamp,
				_config.From,
				_config.To
			);
		}
		OnProgress?.Invoke();
	}

	public void Dispose()
	{
		_timer.Dispose();
		lock (_lock)
		{
			_reader?.Dispose();
		}
		_logger.LogInformation("Simulation engine disposed");
	}
}
