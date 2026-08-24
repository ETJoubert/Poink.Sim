using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Poink.Sim.Brain;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Trader;
using Poink.Sim.Trader.Risk;

namespace Poink.Sim.Simulation;

/// <summary>
/// Interactive replay driver for one shared, in-process simulation run: plays, pauses and
/// steps ticks through the shared aggregator, brain and trader, and publishes an immutable
/// snapshot after each callback for the UI to render.
/// </summary>
internal sealed class SimulationEngine : ISimulationEngine
{
	private const int CallbackIntervalMs = 100;
	private const int BaseUnitsPerCallback = 5;
	private const int FiniteSpeedUnitsCap = 100_000; // headroom above 5 * MaxFiniteMultiplier
	private const double MaxFiniteMultiplier = 10_000;
	private const int UncappedSliceMs = 100; // time budget per callback while uncapped, not a tick cap

	private const int MaxRecentBars = 300;
	private const int MaxRecentTicks = 5000;
	private const int MaxRecentLog = 200;

	private const double DefaultPriceBucketWidth = 0.01;

	private static double PriceBucketWidthFor(string table) => DefaultPriceBucketWidth;

	private readonly object _lock = new();
	private readonly BrainRegistry _brainRegistry;
	private readonly Timer _timer;

	private sealed class TraderSession
	{
		public required int Id;
		public required string Label;
		public required Trader.ITrader Trader;
		public readonly RingBuffer<SimulationLogEntry> RecentLog = new(MaxRecentLog);
	}

	private Func<IEnumerator<Tick>>? _openReader;
	private IEnumerator<Tick>? _reader;
	private int _cursor;
	private int _totalCount;
	private SimulationConfig? _config;
	private SimulationBarAggregator? _aggregator;
	private PriceCellTracker? _cellTracker;
	private int _cellStepCount;
	private (long Cell, double Low, double High)? _lastCellStep;
	private DateTime? _lastCellStepUtc;
	private List<TraderSession> _traderSessions = new();
	private ISimulationBrain? _activeBrain;
	private double _speedMultiplier = 1.0;
	private bool _uncapped;
	private bool _isPlaying;

	private int? _selectedTraderIdForIo;

	private RiskPolicyToggles? _riskToggles;

	private readonly RingBuffer<HlocAvdtBar> _recentBars = new(MaxRecentBars);
	private readonly RingBuffer<BarPriceHistogram> _recentBarHistograms = new(MaxRecentBars);
	private readonly RingBuffer<Tick> _recentTicks = new(MaxRecentTicks);

	private double? _lastPrice;
	private double? _lastBid;
	private double? _lastAsk;
	private DateTime? _lastTimestamp;

	private readonly ITickRepository _tickRepository;
	private readonly Trader.ITraderFactory _traderFactory;
	private readonly IRiskPolicyService _riskPolicyService;
	private readonly ILogger<SimulationEngine> _logger;

	public SimulationEngine(
		BrainRegistry brainRegistry,
		ITickRepository tickRepository,
		Trader.ITraderFactory traderFactory,
		IRiskPolicyService riskPolicyService,
		ILogger<SimulationEngine> logger
	)
	{
		_brainRegistry = brainRegistry;
		_tickRepository = tickRepository;
		_traderFactory = traderFactory;
		_riskPolicyService = riskPolicyService;
		_logger = logger;
		_timer = new Timer(TimerCallback, null, Timeout.Infinite, Timeout.Infinite);
	}

	public IReadOnlyList<string> BrainNames => _brainRegistry.All.Select(b => b.Name).ToList();
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
				long count = _tickRepository.CountTicks(config.Table, config.From, config.To);
				if (count <= 0)
					throw new InvalidOperationException("no ticks in the selected range");

				IEnumerator<Tick> OpenReader() =>
					_tickRepository.ReadTicks(config.Table, config.From, config.To).GetEnumerator();
				FinishLoad(config, (int)Math.Min(count, int.MaxValue), OpenReader, "historic");
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

	public bool TryLoadDummy(SimulationConfig config, int tickCount = 20_000)
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
				var ticks = DummyTickGenerator.Generate(config.From, config.To, tickCount);
				if (ticks.Count == 0)
					throw new InvalidOperationException("no ticks in the selected range");
				FinishLoad(config, ticks.Count, () => ticks.GetEnumerator(), "dummy");
			}
			catch (Exception ex)
			{
				lock (_lock)
				{
					IsLoading = false;
					Error = ex.Message;
				}
				_logger.LogError(ex, "Failed to load dummy simulation");
			}
			PublishSnapshot();
		});

		return true;
	}

	private void FinishLoad(SimulationConfig config, int totalCount, Func<IEnumerator<Tick>> openReader, string source)
	{
		var brains = _brainRegistry.All;
		var brain = brains.FirstOrDefault(b => b.Name == config.BrainName) ?? brains.FirstOrDefault();
		if (brain is null)
			throw new InvalidOperationException("no simulation brain registered");

		if (brain is IPolicyGradientBrain inferenceBrain)
			inferenceBrain.MoveToInferenceDevice();

		lock (_lock)
		{
			_timer.Change(Timeout.Infinite, Timeout.Infinite);
			_reader?.Dispose();
			_openReader = openReader;
			_reader = openReader();
			_cursor = 0;
			_totalCount = totalCount;
			_config = config;
			_riskToggles = _riskPolicyService.CreateToggles(config.RiskPolicyNames, config.LockRiskPolicies);
			_traderSessions = CreateSessions(
				config.TraderCount,
				config.StartingCash,
				BuildTraderSettings(config, _riskToggles, brain)
			);
			_selectedTraderIdForIo = _traderSessions.Count > 0 ? _traderSessions[0].Id : null;
			UpdateCaptureIoLocked();
			_aggregator = config.Aggregate
				? new SimulationBarAggregator(config.BarSize, PriceBucketWidthFor(config.Table))
				: null;
			_cellTracker = BuildCellTracker(config);
			_cellStepCount = 0;
			_lastCellStep = null;
			_lastCellStepUtc = null;
			_activeBrain = brain;
			_activeBrain.Reset();
			_recentBars.Clear();
			_recentBarHistograms.Clear();
			_recentTicks.Clear();
			_lastPrice = null;
			_lastBid = null;
			_lastAsk = null;
			_lastTimestamp = null;
			_isPlaying = false;
			IsLoading = false;
		}

		_logger.LogInformation(
			"Loaded {Source} simulation: table={Table}, from={From}, to={To}, brain={Brain}, ticks={TotalCount}",
			source,
			config.Table,
			config.From,
			config.To,
			brain.Name,
			totalCount
		);
	}

	private List<TraderSession> CreateSessions(int count, double startingCash, Trader.TraderSettings settings) =>
		Enumerable
			.Range(1, Math.Max(1, count))
			.Select(i => new TraderSession
			{
				Id = i,
				Label = $"Trader {i}",
				Trader = _traderFactory.Create(startingCash, settings),
			})
			.ToList();

	// The same brain instance is shared by every trader in the run (stateless decision function
	// over its observation, guarded internally by its own forward-pass lock), not per-trader-owned.
	private static Trader.TraderSettings BuildTraderSettings(
		SimulationConfig config,
		RiskPolicyToggles? toggles,
		ISimulationBrain? brain
	) =>
		new(
			config.Leverage,
			config.StopOutLevelPercent,
			new Trader.Commission.PercentOfNotionalCommission(config.CommissionRatePercent),
			config.RiskPolicyNames,
			toggles,
			brain
		);

	private void UpdateCaptureIoLocked()
	{
		foreach (var session in _traderSessions)
			session.Trader.CaptureIo = session.Id == _selectedTraderIdForIo;
	}

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
				var aggregator = new SimulationBarAggregator(barSize, PriceBucketWidthFor(_config.Table));
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

	public bool SetSteppingMode(SimulationSteppingMode mode, double cellWidth)
	{
		lock (_lock)
		{
			if (_config is null || !_config.Aggregate || cellWidth <= 0)
				return false;
			if (_config.SteppingMode == mode && Math.Abs(_config.CellWidth - cellWidth) < double.Epsilon)
				return false;

			_config = _config with { SteppingMode = mode, CellWidth = cellWidth };
			_cellTracker = BuildCellTracker(_config);
			_cellStepCount = 0;
			_lastCellStep = null;
			_lastCellStepUtc = null;
		}
		PublishSnapshot();
		_logger.LogInformation("Simulation stepping mode changed: mode={Mode}, cellWidth={CellWidth}", mode, cellWidth);
		return true;
	}

	private static PriceCellTracker? BuildCellTracker(SimulationConfig config) =>
		config.Aggregate && config.SteppingMode == SimulationSteppingMode.Cell
			? new PriceCellTracker(config.CellWidth)
			: null;

	public bool Reset()
	{
		lock (_lock)
		{
			if (_config is null || _openReader is null)
				return false;
			_timer.Change(Timeout.Infinite, Timeout.Infinite);
			_reader?.Dispose();
			_reader = _openReader();
			_cursor = 0;
			_traderSessions = CreateSessions(
				_config.TraderCount,
				_config.StartingCash,
				BuildTraderSettings(_config, _riskToggles, _activeBrain)
			);
			_selectedTraderIdForIo = _traderSessions.Count > 0 ? _traderSessions[0].Id : null;
			UpdateCaptureIoLocked();
			_aggregator = _config.Aggregate
				? new SimulationBarAggregator(_config.BarSize, PriceBucketWidthFor(_config.Table))
				: null;
			_cellTracker = BuildCellTracker(_config);
			_cellStepCount = 0;
			_lastCellStep = null;
			_lastCellStepUtc = null;
			_activeBrain?.Reset();
			_recentBars.Clear();
			_recentBarHistograms.Clear();
			_recentTicks.Clear();
			_lastPrice = null;
			_lastBid = null;
			_lastAsk = null;
			_lastTimestamp = null;
			_isPlaying = false;
		}
		PublishSnapshot();
		_logger.LogInformation("Simulation reset to start of range");
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
			_config = null;
			_riskToggles = null;
			_traderSessions = new();
			_aggregator = null;
			_cellTracker = null;
			_cellStepCount = 0;
			_lastCellStep = null;
			_lastCellStepUtc = null;
			_activeBrain = null;
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
		}

		if (_aggregator is not null)
		{
			var completed = _aggregator.Feed(tick);
			if (completed is not null)
				AppendBar(completed.Value.Bar, completed.Value.Histogram);

			if (_cellTracker is not null)
			{
				if (_cellTracker.Feed(tick) is { } step)
				{
					_cellStepCount++;
					_lastCellStep = step;
					_lastCellStepUtc = tick.Timestamp;
					ObserveAndApply(tick.Timestamp, price, tick.Bid, tick.Ask, useBars: true);
				}
			}
			else if (completed is not null)
			{
				ObserveAndApply(tick.Timestamp, completed.Value.Bar.Close, tick.Bid, tick.Ask, useBars: true);
			}
		}
		else
		{
			ObserveAndApply(tick.Timestamp, price, tick.Bid, tick.Ask, useBars: false);
		}
	}

	private void ObserveAndApply(DateTime timestamp, double price, double bid, double ask, bool useBars)
	{
		var window = new MarketWindow(
			timestamp,
			price,
			bid,
			ask,
			useBars ? _recentBars : Array.Empty<HlocAvdtBar>(),
			useBars ? Array.Empty<Tick>() : _recentTicks,
			useBars ? _recentBarHistograms : Array.Empty<BarPriceHistogram>()
		);

		foreach (var session in _traderSessions)
		{
			var execution = session.Trader.Observe(window);
			if (execution is { } exec)
				AppendLog(session, LogEntryFor(exec, price, timestamp));
		}
	}

	public bool SetTraderMode(int traderId, TraderControlMode mode)
	{
		lock (_lock)
		{
			var session = _traderSessions.FirstOrDefault(s => s.Id == traderId);
			if (session is null)
				return false;
			session.Trader.ControlMode = mode;
		}
		PublishSnapshot();
		return true;
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

	public void SelectTraderForIo(int? traderId)
	{
		lock (_lock)
		{
			_selectedTraderIdForIo = traderId;
			UpdateCaptureIoLocked();
		}
	}

	// Submitting a manual decision seizes control immediately, whatever the trader's current
	// mode -- the Agent goes quiet (ControlMode != Auto) until the user flips back.
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
			session.Trader.ControlMode = TraderControlMode.Manual;
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

			var cellState =
				_cellTracker is not null && _lastCellStep is { } step && _lastCellStepUtc is { } stepTime
					? new SimulationCellState(step.Cell, step.Low, step.High, _cellStepCount, stepTime)
					: null;

			snapshot =
				_config is null || _reader is null
					? null
					: new SimulationSnapshot(
						_config,
						_isPlaying,
						_cursor >= _totalCount,
						_speedMultiplier,
						_uncapped,
						_cursor,
						_totalCount,
						_lastTimestamp,
						_lastPrice,
						_lastBid,
						_lastAsk,
						_traderSessions
							.Select(ts => new TraderSnapshot(
								ts.Id,
								ts.Label,
								ts.Trader.ControlMode,
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
								ts.Trader.LatestInputs,
								ts.Trader.LatestOutputs,
								ts.Trader is IRiskInspectable inspectable
									? inspectable.PolicyStatuses()
									: Array.Empty<RiskPolicyStatus>(),
								ts.Trader is IRiskInspectable denialInspectable ? denialInspectable.LastDenial : null
							))
							.ToList(),
						bars,
						barHistograms,
						_recentTicks.ToList(),
						primaryRisk?.PolicyStates() ?? Array.Empty<RiskPolicySnapshot>(),
						primaryRisk?.PoliciesLocked ?? false,
						cellState
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
			Progress = new SimulationProgress(_cursor, _totalCount, _lastTimestamp, _config.From, _config.To);
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
