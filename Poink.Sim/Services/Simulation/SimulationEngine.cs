// ── Orchestrating singleton: MarketSim + Aggregator + Brain + Trader ────
//  Real historic runs stream ticks straight from Postgres via a live,
//  forward-only ITickRepository.ReadTicks reader -- this engine never
//  materializes the full selected range in memory (see ITickRepository's
//  ReadTicks doc comment: already a lazy Dapper `buffered: false` iterator).
//  Playback only ever moves the reader forward; Reset disposes it and opens a
//  fresh one from the start of the configured range. TryLoadDummy's synthetic
//  ticks are a small in-memory List<Tick> instead (never touches Postgres),
//  wrapped behind the same reader abstraction so AdvanceOneUnitLocked/Reset
//  don't need to know which source they're playing back.
//  Playback runs on one System.Threading.Timer per engine, used as
//  a self-rescheduling one-shot (period = Infinite; the callback re-arms
//  itself at the end) so callbacks never overlap and Pause/SetSpeed take
//  effect cleanly at a callback boundary instead of racing a running one.
//
//  Speed controls how many ticks advance per callback, not how fast the
//  timer fires (fixed ~100ms cadence) -- this bounds OnChanged/StateHasChanged
//  traffic to ~10/sec regardless of speed, so a high multiplier plays through
//  history faster without flooding the Blazor Server circuit.
//
//  SetMaxSpeed() ("Max" on the page) is a separate uncapped mode, not just a
//  bigger multiplier: TimerCallback drains ticks for a fixed time slice
//  (UncappedSliceMs) instead of a bounded tick count, immediately rescheduling
//  for the next slice -- as fast as the reader/brain allow overall, but still
//  publishing a snapshot roughly every slice so the page's timeline/stats
//  visibly progress instead of freezing until one final publish. Hiding the
//  chart/tick table for faster rendering during a Max run is a separate,
//  independent page-side toggle (Hide Chart) -- the engine has no concept of
//  "hidden".
//
//  Trader.Advance (mark-to-market plus SL/TP/stop-out/pending-order-trigger
//  checks) runs unconditionally every raw tick, regardless of the aggregation
//  toggle -- those need real intrabar precision. Brain.Decide + Trader.Apply
//  run once per raw tick when aggregation is off, or once per completed bar
//  when aggregation is on -- this is what makes the aggregation toggle
//  actually control what the Brain sees.
//
//  Single global instance, like every other *TrainingService singleton in
//  Poink.ML: one shared in-memory "current run" for the whole process, not
//  per-user/session.
// ─────────────────────────────────────────────────────────────────────────
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

namespace Poink.Sim.Simulation;

internal sealed class SimulationEngine : IDisposable
{
    private const int CallbackIntervalMs = 100;
    private const int BaseUnitsPerCallback = 5;
    private const int FiniteSpeedUnitsCap = 100_000; // headroom above 5 * MaxFiniteMultiplier
    private const double MaxFiniteMultiplier = 10_000;
    private const int UncappedSliceMs = 100; // time budget per callback while uncapped, not a tick cap

    private const int MaxRecentBars = 300;
    private const int MaxRecentTicks = 5000;
    private const int MaxRecentLog = 200;

    // Per-symbol price precision for the bar-mode volume-profile histogram (see
    // BarPriceHistogram) -- every symbol uses this same 2-decimal-place width today;
    // this is the single seam to swap in a real per-symbol lookup (pip size /
    // decimal places) later without touching SimulationBarAggregator or its callers.
    private const double DefaultPriceBucketWidth = 0.01;

    private static double PriceBucketWidthFor(string table) => DefaultPriceBucketWidth;

    private readonly object _lock = new();
    private readonly BrainRegistry _brainRegistry;
    private readonly Timer _timer;

    // One per configured trader -- all sessions share the single _activeBrain
    // instance (stateless today, see ISimulationBrain), but each owns its own
    // Trader (Capital/Position) and its own recent-log buffer. Trader is typed
    // as ITrader (not the concrete class) so this session -- and DecideAndApply's
    // orchestration of it -- can be exercised against a fake/mock in a unit test.
    private sealed class TraderSession
    {
        public required int Id;
        public required string Label;
        public required Trader.ITrader Trader;
        public TraderControlMode Mode = TraderControlMode.Auto;
        public readonly RingBuffer<SimulationLogEntry> RecentLog = new(MaxRecentLog);

        // This session's most recently captured brain I/O -- only ever set for
        // whichever session _selectedTraderIdForIo currently designates (see
        // DecideAndApply/SelectTraderForIo); every other session's stays null.
        public IReadOnlyList<RlInputFeature>? LatestInputs;
        public IReadOnlyList<RlOutputFeature>? LatestOutputs;
    }

    // _openReader recreates a fresh forward-only reader from the start of the
    // configured range (Postgres: a new ITickRepository.ReadTicks call --
    // Dummy: a fresh GetEnumerator() over the already-materialized synthetic
    // list) -- Reset calls it again instead of trying to rewind _reader itself.
    private Func<IEnumerator<Tick>>? _openReader;
    private IEnumerator<Tick>? _reader;
    private int _cursor;
    private int _totalCount;
    private SimulationConfig? _config;
    private SimulationBarAggregator? _aggregator;
    private List<TraderSession> _traderSessions = new();
    private ISimulationBrain? _activeBrain;
    private double _speedMultiplier = 1.0;
    private bool _uncapped;
    private bool _isPlaying;

    // Which trader session DecideAndApply captures brain Inputs/Outputs for
    // (see TraderSession.LatestInputs/LatestOutputs) -- defaults to the first
    // session whenever _traderSessions is (re)built, matching SimulationPage's
    // own "falls back to FirstOrDefault()" default for its SelectedTrader.
    private int? _selectedTraderIdForIo;

    private readonly RingBuffer<HlocAvdtBar> _recentBars = new(MaxRecentBars);
    private readonly RingBuffer<BarPriceHistogram> _recentBarHistograms = new(MaxRecentBars);
    private readonly RingBuffer<Tick> _recentTicks = new(MaxRecentTicks);

    private double? _lastPrice;
    private double? _lastBid;
    private double? _lastAsk;
    private DateTime? _lastTimestamp;

    private readonly ITickRepository _tickRepository;
    private readonly Trader.ITraderFactory _traderFactory;
    private readonly ILogger<SimulationEngine> _logger;

    public SimulationEngine(
        BrainRegistry brainRegistry, ITickRepository tickRepository, Trader.ITraderFactory traderFactory, ILogger<SimulationEngine> logger)
    {
        _brainRegistry = brainRegistry;
        _tickRepository = tickRepository;
        _traderFactory = traderFactory;
        _logger = logger;
        _timer = new Timer(TimerCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    public IReadOnlyList<string> BrainNames => _brainRegistry.All.Select(b => b.Name).ToList();
    public SimulationSnapshot? Snapshot { get; private set; }
    public bool IsLoading { get; private set; }
    public string? Error { get; private set; }

    // Cheap cursor/timestamp-only read-model, updated in lockstep with every
    // Snapshot publish plus (during uncapped Max slices) far more often than
    // Snapshot is -- see PublishProgress. Lets a standalone timeline component
    // subscribe to OnProgress alone and stay live through a Max run without
    // pulling in Snapshot's expensive recent-ticks/bars/trader-log copies, so
    // Max doesn't pay for a full page re-render on every slice.
    public SimulationProgress? Progress { get; private set; }

    public event Action? OnChanged;
    public event Action? OnProgress;

    // Streams straight from Postgres -- picking a symbol+range is enough, no
    // separate "LOAD DATA" preload step. TotalCount comes from a single
    // chunk-pruned COUNT(*) (cheap regardless of table size); actual rows are
    // read lazily, one at a time, as playback advances (see AdvanceOneUnitLocked).
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

                IEnumerator<Tick> OpenReader() => _tickRepository.ReadTicks(config.Table, config.From, config.To).GetEnumerator();
                FinishLoad(config, (int)Math.Min(count, int.MaxValue), OpenReader, "historic");
            }
            catch (Exception ex)
            {
                lock (_lock)
                { IsLoading = false; Error = ex.Message; }
                _logger.LogError(ex, "Failed to load simulation for table {Table}", config.Table);
            }
            PublishSnapshot();
        });

        return true;
    }

    // Bypasses Postgres entirely with a synthetic random-walk tick series, so the
    // playback UI can be exercised without a running database. Table/From/To in
    // config are cosmetic here (still shown in the header/progress text) -- the
    // tick count/timespan actually played is GenerateDummyTicks's, not the DB's.
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
                { IsLoading = false; Error = ex.Message; }
                _logger.LogError(ex, "Failed to load dummy simulation");
            }
            PublishSnapshot();
        });

        return true;
    }

    // Shared by TryLoad/TryLoadDummy once a total count is known and a fresh
    // reader can be opened on demand -- openReader is stored so Reset can call
    // it again to rewind, without either caller needing to know that. Runs off
    // the UI thread inside the Task.Run callers above.
    private void FinishLoad(SimulationConfig config, int totalCount, Func<IEnumerator<Tick>> openReader, string source)
    {
        var brains = _brainRegistry.All;
        var brain = brains.FirstOrDefault(b => b.Name == config.BrainName) ?? brains.FirstOrDefault();
        if (brain is null)
            throw new InvalidOperationException("no simulation brain registered");

        // See IPolicyGradientBrain's device-placement doc comment -- outside
        // _lock since it's independent of this engine's own state, and safe
        // because this app's usage is train-then-simulate, never simulating a
        // brain while RlTrainingService is mid-training it.
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
            _traderSessions = CreateSessions(config.TraderCount, config.StartingCash, BuildTraderSettings(config));
            _selectedTraderIdForIo = _traderSessions.Count > 0 ? _traderSessions[0].Id : null;
            _aggregator = config.Aggregate
                ? new SimulationBarAggregator(config.BarSize, PriceBucketWidthFor(config.Table))
                : null;
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
            source, config.Table, config.From, config.To, brain.Name, totalCount);
    }

    private List<TraderSession> CreateSessions(int count, double startingCash, Trader.TraderSettings settings) =>
        Enumerable.Range(1, Math.Max(1, count))
            .Select(i => new TraderSession { Id = i, Label = $"Trader {i}", Trader = _traderFactory.Create(startingCash, settings) })
            .ToList();

    private static Trader.TraderSettings BuildTraderSettings(SimulationConfig config) => new(
        config.Leverage, config.StopOutLevelPercent,
        new Trader.Commission.PercentOfNotionalCommission(config.CommissionRatePercent));

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
        int cursor, total;
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
        int cursor, total;
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
        { _uncapped = false; _speedMultiplier = Math.Clamp(multiplier, 0.1, MaxFiniteMultiplier); }
        PublishSnapshot();
    }

    // Uncapped: TimerCallback drains every remaining tick in one callback instead
    // of a bounded slice per callback -- see the class-level doc comment.
    public void SetMaxSpeed()
    {
        lock (_lock)
        { _uncapped = true; }
        PublishSnapshot();
    }

    // Swaps the live bar-aggregation mode/size without restarting playback -- takes
    // effect at the very next tick. Rather than blanking the chart, a fresh
    // aggregator replays it against _recentTicks (bars under the old size can't be
    // merged with the new ones -- BarPriceHistogram assumes one BucketWidth per run
    // -- so they're recomputed, not kept) to rebuild as much bar history as that
    // retained tick window covers; older history than that is simply gone, same as
    // it already was for the tick view. Recent ticks themselves, trader state, and
    // the brain are untouched, so no position/equity is lost.
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
        _logger.LogInformation("Simulation aggregation changed: aggregate={Aggregate}, barSize={BarSize}", aggregate, barSize);
        return true;
    }

    // Rewinds to the start of the same loaded config with a fresh Trader/Aggregator/Brain.
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
            _traderSessions = CreateSessions(_config.TraderCount, _config.StartingCash, BuildTraderSettings(_config));
            _selectedTraderIdForIo = _traderSessions.Count > 0 ? _traderSessions[0].Id : null;
            _aggregator = _config.Aggregate
                ? new SimulationBarAggregator(_config.BarSize, PriceBucketWidthFor(_config.Table))
                : null;
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
            _traderSessions = new();
            _aggregator = null;
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
                // Time-boxed rather than "drain everything in one shot": still as
                // fast as the reader/brain allow, but sliced so a multi-million-tick
                // run keeps advancing visibly instead of appearing frozen until the
                // very end.
                var sw = Stopwatch.StartNew();
                while (_cursor < _totalCount && sw.ElapsedMilliseconds < UncappedSliceMs)
                    AdvanceOneUnitLocked();

                if (_cursor >= _totalCount)
                    _isPlaying = false;
                reschedule = _isPlaying;
            }
            else
            {
                int units = Math.Clamp((int)Math.Round(BaseUnitsPerCallback * _speedMultiplier), 1, FiniteSpeedUnitsCap);
                for (int i = 0; i < units && _cursor < _totalCount; i++)
                    AdvanceOneUnitLocked();

                if (_cursor >= _totalCount)
                    _isPlaying = false;
                reschedule = _isPlaying;
            }
        }

        // Mid-run Max slices only get the cheap progress-only publish (just cursor/
        // timestamp -- no recent-ticks/bars/trader-log copies) so a timeline
        // indicator can track them live without forcing a full page re-render on
        // every slice; everything else catches up in one full publish once the
        // run pauses/finishes, same as Max always has.
        if (wasUncapped && reschedule)
            PublishProgress();
        else
            PublishSnapshot();

        if (reschedule)
            _timer.Change(wasUncapped ? 0 : CallbackIntervalMs, Timeout.Infinite);
    }

    // Must be called while holding _lock. Forward-only: playback never seeks
    // backwards, only Reset/a fresh load restart the reader from the beginning.
    private void AdvanceOneUnitLocked()
    {
        if (!_reader!.MoveNext())
        {
            // Reader ran dry before totalCount (a defensive fallback -- e.g. rows
            // deleted between the upfront COUNT and now) -- treat as finished
            // rather than looping forever.
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

        // Always recorded, independent of the aggregation toggle -- the recent-ticks
        // buffer feeds the volume-profile panel even when the Brain itself is only
        // being shown bars (DecideAndApply below still passes it Array.Empty<Tick>()
        // in that case, so Brain behavior is unchanged).
        AppendTick(tick);

        // Unconditional every raw tick, independent of the aggregation toggle --
        // SL/TP, stop-out, and a resting pending order all need real intrabar
        // precision, not just once-per-decision-point precision. Each session's
        // resulting CapitalState is carried into DecideAndApply below so
        // Trader.Snapshot isn't computed a second time for the same tick.
        var capitalStates = new List<CapitalState>(_traderSessions.Count);
        foreach (var session in _traderSessions)
        {
            var maintenance = session.Trader.Advance(tick.Bid, tick.Ask, tick.Timestamp);
            capitalStates.Add(maintenance.Capital);
            foreach (var fill in maintenance.AutomaticFills)
                AppendLog(session, new SimulationLogEntry(
                    tick.Timestamp, fill.Action.ToString(), price, fill.Direction, fill.Size, fill.RealizedPnl, fill.Commission, fill.Reason));
        }

        if (_aggregator is not null)
        {
            var completed = _aggregator.Feed(tick);
            if (completed is not null)
            {
                AppendBar(completed.Value.Bar, completed.Value.Histogram);
                DecideAndApply(tick.Timestamp, completed.Value.Bar.Close, tick.Bid, tick.Ask, useBars: true, capitalStates);
            }
        }
        else
        {
            DecideAndApply(tick.Timestamp, price, tick.Bid, tick.Ask, useBars: false, capitalStates);
        }
    }

    // Must be called while holding _lock. Every Auto-mode trader session decides/
    // applies against the same shared _activeBrain instance -- see the TraderSession
    // doc comment above. A Manual-mode session is skipped here entirely (it only
    // moves via SubmitManualDecision, called from the page's trader controls panel)
    // -- Trader.Advance (mark-to-market/SL-TP/stop-out/pending-trigger) still ran
    // for it in AdvanceOneUnitLocked regardless of mode, just not this Brain step.
    // capitalStates is index-aligned with _traderSessions, already computed by this
    // tick's Advance call (see AdvanceOneUnitLocked).
    private void DecideAndApply(DateTime timestamp, double price, double bid, double ask, bool useBars, IReadOnlyList<CapitalState> capitalStates)
    {
        for (int i = 0; i < _traderSessions.Count; i++)
        {
            var session = _traderSessions[i];
            if (session.Mode == TraderControlMode.Manual)
                continue;

            var capital = capitalStates[i];
            var observation = new SimulationObservation(
                timestamp, price,
                useBars ? _recentBars : Array.Empty<HlocAvdtBar>(),
                useBars ? Array.Empty<Tick>() : _recentTicks,
                useBars ? _recentBarHistograms : Array.Empty<BarPriceHistogram>(),
                capital, session.Trader.CurrentPosition, bid, ask);

            // Capturing Inputs/Outputs for every session on every decision would
            // multiply the extra DescribeInputs/DecideWithLogProbability cost by
            // TraderCount for no benefit, since only one trader's I/O is ever
            // shown at a time (see SimulationPage's INPUTS/OUTPUTS tab) -- scoped
            // to whichever session _selectedTraderIdForIo currently designates.
            // ComposedPolicyBrain.Decide already runs this same forward pass and
            // computes Outputs internally then discards them, so capturing here
            // costs zero extra forward passes for Outputs; DescribeInputs is one
            // extra (cheap, no autograd/forward pass) call.
            SimulationDecision decision;
            if (_activeBrain is IPolicyGradientBrain policyBrain && session.Id == _selectedTraderIdForIo)
            {
                var (decidedAction, _, _, outputs) = policyBrain.DecideWithLogProbability(observation);
                session.LatestInputs = policyBrain.DescribeInputs(observation);
                session.LatestOutputs = outputs;
                decision = decidedAction;
            }
            else
            {
                decision = _activeBrain!.Decide(observation);
            }

            var execution = session.Trader.Apply(decision, bid, ask, timestamp);
            if (execution is { } exec)
                AppendLog(session, new SimulationLogEntry(
                    timestamp, exec.Action.ToString(), price, exec.Direction, exec.Size, exec.RealizedPnl, exec.Commission, exec.Reason));
        }
    }

    // Toggles one trader session between Brain-driven (Auto) and human-driven
    // (Manual) control. Switching to Manual takes effect on the *next* decision
    // point -- there's no in-flight Brain decision to reconcile, since Decide()
    // is a stateless, point-in-time call (see ISimulationBrain). Switching back to
    // Auto doesn't touch any position/pending order a manual session already has
    // open -- the Brain just resumes deciding for it from wherever it stands.
    public bool SetTraderMode(int traderId, TraderControlMode mode)
    {
        lock (_lock)
        {
            var session = _traderSessions.FirstOrDefault(s => s.Id == traderId);
            if (session is null)
                return false;
            session.Mode = mode;
        }
        PublishSnapshot();
        return true;
    }

    // Which trader session DecideAndApply captures brain Inputs/Outputs for --
    // called by SimulationPage whenever the user picks a different trader on
    // the TRADERS tab, so the INPUTS/OUTPUTS tab follows the same selection
    // (see TraderSession.LatestInputs/LatestOutputs). No PublishSnapshot here
    // -- switching selection doesn't itself produce new data to show (the
    // newly-selected session's I/O, if any, only starts populating from the
    // next decision point onward), so there's nothing for a republish to
    // reveal early.
    public void SelectTraderForIo(int? traderId)
    {
        lock (_lock) { _selectedTraderIdForIo = traderId; }
    }

    // Applies a manually-constructed decision (built by the trader controls panel)
    // to one Manual-mode session, bypassing the Brain entirely -- rejected if that
    // session isn't in Manual mode (must SetTraderMode first), so a human and the
    // Brain never issue conflicting decisions for the same session. Executes at the
    // engine's last known Bid/Ask/timestamp, same price basis DecideAndApply uses.
    public bool SubmitManualDecision(int traderId, SimulationDecision decision)
    {
        lock (_lock)
        {
            if (_config is null)
                return false;
            var session = _traderSessions.FirstOrDefault(s => s.Id == traderId);
            if (session is null || session.Mode != TraderControlMode.Manual)
                return false;

            double bid = _lastBid ?? 0, ask = _lastAsk ?? 0;
            var timestamp = _lastTimestamp ?? DateTime.UtcNow;
            var execution = session.Trader.Apply(decision, bid, ask, timestamp);
            if (execution is { } exec)
                AppendLog(session, new SimulationLogEntry(
                    timestamp, exec.Action.ToString(), (bid + ask) / 2.0, exec.Direction, exec.Size, exec.RealizedPnl, exec.Commission, exec.Reason));
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

    private void PublishSnapshot()
    {
        SimulationSnapshot? snapshot;
        lock (_lock)
        {
            // The in-progress bar is appended after the completed ones so the chart's last
            // candle updates tick-by-tick instead of only snapping into place once it closes.
            // Its histogram rides alongside, index-aligned with bars, for the same reason.
            var bars = _recentBars.ToList();
            var barHistograms = _recentBarHistograms.ToList();
            if (_aggregator?.CurrentPartialBar is { } partial)
            {
                bars.Add(partial);
                barHistograms.Add(_aggregator.CurrentPartialHistogram!);
            }

            snapshot = _config is null || _reader is null
                ? null
                : new SimulationSnapshot(
                    _config, _isPlaying, _cursor >= _totalCount, _speedMultiplier, _uncapped,
                    _cursor, _totalCount, _lastTimestamp, _lastPrice, _lastBid, _lastAsk,
                    _traderSessions.Select(ts => new TraderSnapshot(
                        ts.Id, ts.Label, ts.Mode, ts.Trader.Snapshot(_lastBid ?? 0, _lastAsk ?? 0), ts.Trader.CurrentPosition,
                        ts.Trader.CurrentPendingOrder, ts.Trader.TradeLog.Count, ts.RecentLog.ToList(), ts.Trader.TradeLog.ToList(),
                        ts.Trader.MaxEquity, ts.Trader.MaxDrawdown, ts.Trader.Leverage, ts.Trader.StopOutLevelPercent,
                        ts.LatestInputs, ts.LatestOutputs)).ToList(),
                    bars, barHistograms, _recentTicks.ToList());
        }
        Snapshot = snapshot;
        Progress = snapshot is null
            ? null
            : new SimulationProgress(snapshot.CursorIndex, snapshot.TotalCount, snapshot.CurrentTimestampUtc,
                snapshot.Config.From, snapshot.Config.To);
        OnChanged?.Invoke();
        OnProgress?.Invoke();
    }

    // Cheap counterpart to PublishSnapshot for mid-Max-run slices -- cursor/
    // timestamp only, none of Snapshot's recent-ticks/bars/trader-log copies.
    // Fires OnProgress alone (not OnChanged), so only a component tracking
    // Progress specifically re-renders; the rest of the page catches up on the
    // next full PublishSnapshot (when the run pauses/finishes).
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
        { _reader?.Dispose(); }
        _logger.LogInformation("Simulation engine disposed");
    }
}
