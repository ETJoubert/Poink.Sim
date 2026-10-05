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

	// time budget per callback while uncapped, not a tick cap; short so controls never wait long on the lock
	private const int UncappedSliceMs = 33;
	private const int UncappedClockCheckTicks = 256;
	private const int UncappedProgressIntervalMs = 100;

	private const int MaxRecentTicks = 5000;
	private const int MaxRecentLog = 200;

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

		private IReadOnlyList<Trade> _publishedTrades = Array.Empty<Trade>();
		private IReadOnlyList<TradeEquityPoint> _publishedTradeEquity = Array.Empty<TradeEquityPoint>();
		private IReadOnlyList<SimulationLogEntry> _publishedRecentLog = Array.Empty<SimulationLogEntry>();
		private long _publishedRecentLogVersion;

		/// <summary>The trade log as last published, copied again only once a trade has been added since.</summary>
		public IReadOnlyList<Trade> PublishedTrades()
		{
			if (_publishedTrades.Count != Trader.TradeLog.Count)
				_publishedTrades = Trader.TradeLog.ToList();
			return _publishedTrades;
		}

		/// <summary>The per-trade equity record as last published, copied again only once a point has been added since.</summary>
		public IReadOnlyList<TradeEquityPoint> PublishedTradeEquity()
		{
			if (_publishedTradeEquity.Count != TradeEquity.Count)
				_publishedTradeEquity = TradeEquity.ToList();
			return _publishedTradeEquity;
		}

		/// <summary>The recent log as last published, copied again only once it has changed since.</summary>
		public IReadOnlyList<SimulationLogEntry> PublishedRecentLog()
		{
			if (_publishedRecentLogVersion != RecentLog.Version)
			{
				_publishedRecentLog = RecentLog.ToList();
				_publishedRecentLogVersion = RecentLog.Version;
			}
			return _publishedRecentLog;
		}
	}

	private Func<IEnumerator<Tick>>? _openReader;
	private IEnumerator<Tick>? _reader;
	private int _cursor;
	private int _totalCount;
	private int _warmupCount;
	private SimulationConfig? _config;
	private readonly SimulationMarketFeed _market = new();
	private List<WatchedSymbolFeed> _watchFeeds = new();
	private List<TraderSession> _traderSessions = new();
	private Dictionary<int, TraderSession> _traderSessionsById = new();
	private double _speedMultiplier = 1.0;
	private bool _uncapped;
	private bool _isPlaying;


	private RiskPolicyToggles? _riskToggles;

	private readonly RingBuffer<Tick> _recentTicks = new(MaxRecentTicks);
	private IReadOnlyList<Tick> _publishedRecentTicks = Array.Empty<Tick>();
	private long _publishedRecentTicksVersion;
	private readonly Stopwatch _progressClock = Stopwatch.StartNew();
	private long _lastProgressPublishMs;

	private double? _lastPrice;
	private double? _lastBid;
	private double? _lastAsk;
	private DateTime? _lastTimestamp;
	private string? _stopReason;

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

	public IReadOnlyDictionary<int, IReadOnlyList<TradeEquityPoint>> TradeEquityByTrader { get; private set; } =
		new Dictionary<int, IReadOnlyList<TradeEquityPoint>>();

	public event Action? OnChanged;
	public event Action? OnProgress;
	public event Action? OnTradeEquityChanged;

	public SimulationFinancialsState? Financials { get; private set; }

	public event Action? OnFinancialsChanged;

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
					new PrefetchingTickReader(() =>
						_tickRepository.ReadTicks(config.Table, config.LoadFrom, config.To).GetEnumerator()
					);
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
			UseSessionsLocked(CreateSessions(config));
			_market.Reset(config.Aggregate, config.BarSize, config.PipSize);
			UseWatchFeedsLocked(CreateWatchFeeds(config));
			_recentTicks.Clear();
			_lastPrice = null;
			_lastBid = null;
			_lastAsk = null;
			_lastTimestamp = null;
			_stopReason = null;
			WarmUpLocked();
			_isPlaying = false;
			IsLoading = false;
		}

		_logger.LogInformation(
			"Loaded {Source} simulation: table={Table}, watched=[{WatchedTables}], from={From}, to={To}, ticks={TotalCount}",
			source,
			config.Table,
			string.Join(", ", config.WatchSymbols?.Select(w => w.Table) ?? []),
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

	// Watched symbols are read over the traded symbol's own range, warm-up included, so their bars line up with its bars.
	private List<WatchedSymbolFeed> CreateWatchFeeds(SimulationConfig config) =>
		(config.WatchSymbols ?? [])
			.Where(w => w.Table != config.Table)
			.DistinctBy(w => w.Table)
			.Select(w => new WatchedSymbolFeed(
				w,
				() => new PrefetchingTickReader(() =>
					_tickRepository.ReadTicks(w.Table, config.LoadFrom, config.To).GetEnumerator()
				)
			))
			.ToList();

	private void UseWatchFeedsLocked(List<WatchedSymbolFeed> feeds)
	{
		foreach (var feed in _watchFeeds)
			feed.Dispose();
		_watchFeeds = feeds;
		foreach (var feed in _watchFeeds)
			feed.Restart(_config!.Aggregate, _config.BarSize);
	}

	private void UseSessionsLocked(List<TraderSession> sessions)
	{
		_traderSessions = sessions;
		_traderSessionsById = sessions.ToDictionary(session => session.Id);
	}

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
			_stopReason = null;
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
			_market.Reaggregate(aggregate, barSize, _config.PipSize, _recentTicks);
			foreach (var feed in _watchFeeds)
				feed.Reaggregate(aggregate, barSize);
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
		UseSessionsLocked(CreateSessions(_config));
		_market.Reset(_config.Aggregate, _config.BarSize, _config.PipSize);
		foreach (var feed in _watchFeeds)
			feed.Restart(_config.Aggregate, _config.BarSize);
		_recentTicks.Clear();
		_lastPrice = null;
		_lastBid = null;
		_lastAsk = null;
		_lastTimestamp = null;
		_stopReason = null;
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

		UseSessionsLocked(CreateSessions(_config));
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
			_stopReason = null;
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
			UseSessionsLocked(new());
			_market.Reset(false, TimeSpan.Zero, 0);
			UseWatchFeedsLocked(new());
			_isPlaying = false;
			_recentTicks.Clear();
			_lastPrice = null;
			_lastBid = null;
			_lastAsk = null;
			_lastTimestamp = null;
			_stopReason = null;
		}
		Snapshot = null;
		Progress = null;
		Error = null;
		Financials = null;
		OnChanged?.Invoke();
		OnProgress?.Invoke();
		PublishTradeEquity();
		OnFinancialsChanged?.Invoke();
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
				while (_isPlaying && _cursor < _totalCount && sw.ElapsedMilliseconds < UncappedSliceMs)
					for (int i = 0; i < UncappedClockCheckTicks && _isPlaying && _cursor < _totalCount; i++)
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
				for (int i = 0; i < units && _isPlaying && _cursor < _totalCount; i++)
					AdvanceOneUnitLocked();

				if (_cursor >= _totalCount)
					_isPlaying = false;
				reschedule = _isPlaying;
			}
		}

		if (!wasUncapped || !reschedule)
			PublishSnapshot();
		else if (IsUncappedProgressDue())
		{
			PublishProgress();
			PublishTradeEquity();
			PublishFinancials();
		}

		if (reschedule)
			_timer.Change(wasUncapped ? 0 : CallbackIntervalMs, Timeout.Infinite);
	}

	private bool IsUncappedProgressDue()
	{
		long now = _progressClock.ElapsedMilliseconds;
		if (now - _lastProgressPublishMs < UncappedProgressIntervalMs)
			return false;
		_lastProgressPublishMs = now;
		return true;
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
		foreach (var feed in _watchFeeds)
			feed.AdvanceTo(tick.Timestamp);

		foreach (var session in _traderSessions)
		{
			var maintenance = session.Trader.Advance(tick.Bid, tick.Ask, tick.Timestamp);
			foreach (var fill in maintenance.AutomaticFills)
				AppendLog(session, LogEntryFor(fill, price, tick.Timestamp));
			TrackTradeEquityLocked(session, tick.Bid, tick.Ask, tick.Timestamp, maintenance.Capital.Equity);
		}

		_market.Feed(tick);
		if (_market.IsAggregating)
			RunBotsLocked(tick, price);

		if (_isPlaying && !_isWarmingUp && AllTradersDepletedLocked(price))
			StopForDepletionLocked();
	}

	// A trader that is flat with no resting order and cannot fund the symbol's minimum volume can never trade again.
	private bool AllTradersDepletedLocked(double price)
	{
		if (_config is null || _traderSessions.Count == 0)
			return false;
		foreach (var session in _traderSessions)
		{
			var trader = session.Trader;
			if (trader.CurrentPosition is not null || trader.CurrentPendingOrder is not null)
				return false;
			double minimumMargin = RiskHelper.MarginRequired(_config.Limits.MinVolume, price, trader.Leverage);
			if (trader.Cash > 0 && trader.Cash >= minimumMargin)
				return false;
		}
		return true;
	}

	private void StopForDepletionLocked()
	{
		_isPlaying = false;
		_stopReason = _traderSessions.Count == 1
			? "Stopped: the account's balance can no longer cover the margin for the symbol's minimum volume"
			: "Stopped: no account's balance can cover the margin for the symbol's minimum volume any more";
		_logger.LogInformation(
			"Simulation stopped at {Cursor}/{Total} because every trader's balance is depleted",
			_cursor - _warmupCount,
			_totalCount - _warmupCount
		);
	}

	// Equity is only sampled while a position is open (and once more when a trade closes), so a long flat
	// stretch costs nothing and every closed trade keeps its own high/low regardless of run length.
	private static void TrackTradeEquityLocked(
		TraderSession session,
		double bid,
		double ask,
		DateTime timestamp,
		double? currentEquity = null
	)
	{
		var trader = session.Trader;
		bool tradeClosed = trader.TradeLog.Count > session.RecordedTradeCount;
		if (trader.CurrentPosition is null && !tradeClosed)
			return;

		double equity = currentEquity ?? trader.Snapshot(bid, ask).Equity;
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
			UseSessionsLocked(CreateSessions(_config));
			_stopReason = null;
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

		var actions = _botService.OnTick(_market.ContextFor(tick, _config));
		foreach (var action in actions)
		{
			var session = _traderSessionsById[action.Bot.Id];
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
			if (!_traderSessionsById.TryGetValue(traderId, out var session))
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
			var bars = _market.RecentBars.ToList(extraCapacity: 1);
			var barHistograms = _market.RecentHistograms.ToList(extraCapacity: 1);
			if (_market.Aggregator?.CurrentPartialBar is { } partial)
			{
				bars.Add(partial);
				barHistograms.Add(_market.Aggregator.CurrentPartialHistogram!);
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
						BuildTraderSnapshotsLocked(),
						bars,
						barHistograms,
						PublishedRecentTicksLocked(),
						primaryRisk?.PolicyStates() ?? Array.Empty<RiskPolicySnapshot>(),
						_market.TypicalSpread,
						_stopReason,
						_watchFeeds.Select(feed => feed.Snapshot()).ToList()
					);
			Financials = snapshot is null ? null : FinancialsStateLocked(snapshot.Traders);
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
		PublishTradeEquity();
		OnFinancialsChanged?.Invoke();
	}

	private List<TraderSnapshot> BuildTraderSnapshotsLocked() =>
		_traderSessions
			.Select(ts => new TraderSnapshot(
				ts.Id,
				ts.Label,
				ts.Trader.Snapshot(_lastBid ?? 0, _lastAsk ?? 0),
				ts.Trader.CurrentPosition,
				ts.Trader.CurrentPendingOrder,
				ts.Trader.TradeLog.Count,
				ts.PublishedRecentLog(),
				ts.PublishedTrades(),
				ts.Trader.MaxEquity,
				ts.Trader.MaxDrawdown,
				ts.Trader.Leverage,
				ts.Trader.StopOutLevelPercent,
				ts.Trader is IRiskInspectable inspectable ? inspectable.PolicyStatuses() : Array.Empty<RiskPolicyStatus>(),
				ts.Trader is IRiskInspectable denialInspectable ? denialInspectable.LastDenial : null,
				ts.PublishedTradeEquity()
			))
			.ToList();

	private SimulationFinancialsState FinancialsStateLocked(IReadOnlyList<TraderSnapshot> traders) =>
		new(traders, _config!.Table, _config.PipSize, _lastBid, _lastAsk, _lastPrice, _lastTimestamp);

	private void PublishFinancials()
	{
		lock (_lock)
		{
			if (_config is null)
				return;
			Financials = FinancialsStateLocked(BuildTraderSnapshotsLocked());
		}
		OnFinancialsChanged?.Invoke();
	}

	private void PublishTradeEquity()
	{
		lock (_lock)
		{
			var published = _traderSessions.ToDictionary(
				session => session.Id,
				session => session.PublishedTradeEquity()
			);
			if (IsSameTradeEquity(published, TradeEquityByTrader))
				return;
			TradeEquityByTrader = published;
		}
		OnTradeEquityChanged?.Invoke();
	}

	private static bool IsSameTradeEquity(
		IReadOnlyDictionary<int, IReadOnlyList<TradeEquityPoint>> current,
		IReadOnlyDictionary<int, IReadOnlyList<TradeEquityPoint>> previous
	) =>
		current.Count == previous.Count
		&& current.All(entry =>
			previous.TryGetValue(entry.Key, out var points) && ReferenceEquals(points, entry.Value)
		);

	private IReadOnlyList<Tick> PublishedRecentTicksLocked()
	{
		if (_publishedRecentTicksVersion != _recentTicks.Version)
		{
			_publishedRecentTicks = _recentTicks.ToList();
			_publishedRecentTicksVersion = _recentTicks.Version;
		}
		return _publishedRecentTicks;
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
			foreach (var feed in _watchFeeds)
				feed.Dispose();
		}
		_logger.LogInformation("Simulation engine disposed");
	}
}
