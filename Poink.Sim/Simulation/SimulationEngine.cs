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
//  Mark-to-market (Trader.Snapshot) runs every raw tick; Brain.Decide +
//  Trader.Apply run once per raw tick when aggregation is off, or once per
//  completed bar when aggregation is on -- this is what makes the
//  aggregation toggle actually control what the Brain sees.
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
    private readonly List<ISimulationBrain> _brains;
    private readonly Timer _timer;

    // One per configured trader -- all sessions share the single _activeBrain
    // instance (stateless today, see ISimulationBrain), but each owns its own
    // Trader (Capital/Position) and its own recent-log buffer.
    private sealed class TraderSession
    {
        public required int Id;
        public required string Label;
        public required Trader.Trader Trader;
        public readonly RingBuffer<SimulationLogEntry> RecentLog = new(MaxRecentLog);
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

    private readonly RingBuffer<HlocAvdtBar> _recentBars = new(MaxRecentBars);
    private readonly RingBuffer<BarPriceHistogram> _recentBarHistograms = new(MaxRecentBars);
    private readonly RingBuffer<Tick> _recentTicks = new(MaxRecentTicks);

    private double? _lastPrice;
    private double? _lastBid;
    private double? _lastAsk;
    private DateTime? _lastTimestamp;

    private readonly ITickRepository _tickRepository;
    private readonly ILogger<SimulationEngine> _logger;

    public SimulationEngine(IEnumerable<ISimulationBrain> brains, ITickRepository tickRepository, ILogger<SimulationEngine> logger)
    {
        _brains = brains.ToList();
        _tickRepository = tickRepository;
        _logger = logger;
        _timer = new Timer(TimerCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    public IReadOnlyList<string> BrainNames => _brains.Select(b => b.Name).ToList();
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
        var brain = _brains.FirstOrDefault(b => b.Name == config.BrainName) ?? _brains.FirstOrDefault();
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
            _traderSessions = CreateSessions(config.TraderCount, config.StartingCash);
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

    private static List<TraderSession> CreateSessions(int count, double startingCash) =>
        Enumerable.Range(1, Math.Max(1, count))
            .Select(i => new TraderSession { Id = i, Label = $"Trader {i}", Trader = new Trader.Trader(startingCash) })
            .ToList();

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
            _traderSessions = CreateSessions(_config.TraderCount, _config.StartingCash);
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

        if (_aggregator is not null)
        {
            var completed = _aggregator.Feed(tick);
            if (completed is not null)
            {
                AppendBar(completed.Value.Bar, completed.Value.Histogram);
                DecideAndApply(tick.Timestamp, completed.Value.Bar.Close, useBars: true);
            }
        }
        else
        {
            DecideAndApply(tick.Timestamp, price, useBars: false);
        }
    }

    // Must be called while holding _lock. Every trader session decides/applies
    // against the same shared _activeBrain instance -- see the TraderSession
    // doc comment above.
    private void DecideAndApply(DateTime timestamp, double price, bool useBars)
    {
        foreach (var session in _traderSessions)
        {
            var capital = session.Trader.Snapshot(price);
            var observation = new SimulationObservation(
                timestamp, price,
                useBars ? _recentBars : Array.Empty<HlocAvdtBar>(),
                useBars ? Array.Empty<Tick>() : _recentTicks,
                useBars ? _recentBarHistograms : Array.Empty<BarPriceHistogram>(),
                capital, session.Trader.CurrentPosition);

            var decision = _activeBrain!.Decide(observation);
            var execution = session.Trader.Apply(decision, price, timestamp);
            if (execution is { } exec)
                AppendLog(session, new SimulationLogEntry(
                    timestamp, decision.Action.ToString(), price, exec.Direction, exec.Size, exec.RealizedPnl, exec.Reason));
        }
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
                        ts.Id, ts.Label, ts.Trader.Snapshot(_lastPrice ?? 0), ts.Trader.CurrentPosition,
                        ts.Trader.TradeLog.Count, ts.RecentLog.ToList(), ts.Trader.TradeLog.ToList(),
                        ts.Trader.MaxEquity, ts.Trader.MaxDrawdown)).ToList(),
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
            if (_config is null) return;
            Progress = new SimulationProgress(_cursor, _totalCount, _lastTimestamp, _config.From, _config.To);
        }
        OnProgress?.Invoke();
    }

    public void Dispose()
    {
        _timer.Dispose();
        lock (_lock) { _reader?.Dispose(); }
        _logger.LogInformation("Simulation engine disposed");
    }
}
