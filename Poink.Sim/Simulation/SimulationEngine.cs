// ── Orchestrating singleton: MarketSim + Aggregator + Brain + Trader ────
//  Ticks are materialized once (by TickDataStore, via the setup screen's "LOAD
//  DATA" button, or generated fresh for TryLoadDummy) and handed to TryLoad as
//  a plain list -- this engine never reads Postgres itself. Played back from
//  memory with an integer cursor, because a pausable/steppable/speed-controlled
//  sim needs random access and can't hold a live DB reader across a pause.
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
//  SetMaxSpeed() ("Max"/"Turbo" on the page) is a separate uncapped mode, not
//  just a bigger multiplier: TimerCallback drains every remaining tick in one
//  callback instead of a bounded slice, so a run finishes in a single publish
//  regardless of how many ticks are left. "Turbo" vs "Max" is purely a page-
//  side rendering choice (Turbo also hides the chart/tick table) -- the engine
//  doesn't know the difference.
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Poink.Sim.Brain;
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
    private const int MaxRecentBars = 300;
    private const int MaxRecentTicks = 500;
    private const int MaxRecentLog = 200;

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
        public readonly List<SimulationLogEntry> RecentLog = new();
    }

    private List<Tick>? _ticks;
    private int _cursor;
    private SimulationConfig? _config;
    private SimulationBarAggregator? _aggregator;
    private List<TraderSession> _traderSessions = new();
    private ISimulationBrain? _activeBrain;
    private double _speedMultiplier = 1.0;
    private bool _uncapped;
    private bool _isPlaying;

    private readonly List<HlocAvdtBar> _recentBars = new();
    private readonly List<Tick> _recentTicks = new();

    private double? _lastPrice;
    private DateTime? _lastTimestamp;

    public SimulationEngine(IEnumerable<ISimulationBrain> brains)
    {
        _brains = brains.ToList();
        _timer = new Timer(TimerCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    public IReadOnlyList<string> BrainNames => _brains.Select(b => b.Name).ToList();
    public SimulationSnapshot? Snapshot { get; private set; }
    public bool IsLoading { get; private set; }
    public string? Error { get; private set; }

    public event Action? OnChanged;

    // ticks must already be materialized (see TickDataStore) -- this no longer
    // touches Postgres itself, so a run can only start once the setup screen's
    // "LOAD DATA" step has actually finished.
    public bool TryLoad(SimulationConfig config, List<Tick> ticks)
    {
        lock (_lock)
        {
            if (IsLoading) return false;
            IsLoading = true;
            Error = null;
        }
        OnChanged?.Invoke();

        _ = Task.Run(() =>
        {
            try
            {
                FinishLoad(config, ticks);
            }
            catch (Exception ex)
            {
                lock (_lock) { IsLoading = false; Error = ex.Message; }
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
            if (IsLoading) return false;
            IsLoading = true;
            Error = null;
        }
        OnChanged?.Invoke();

        _ = Task.Run(() =>
        {
            try
            {
                var ticks = DummyTickGenerator.Generate(config.From, config.To, tickCount);
                FinishLoad(config, ticks);
            }
            catch (Exception ex)
            {
                lock (_lock) { IsLoading = false; Error = ex.Message; }
            }
            PublishSnapshot();
        });

        return true;
    }

    // Shared by TryLoad/TryLoadDummy once a tick list is in hand (from Postgres or
    // generated). Runs off the UI thread inside the Task.Run callers above.
    private void FinishLoad(SimulationConfig config, List<Tick> ticks)
    {
        if (ticks.Count == 0)
            throw new InvalidOperationException("no ticks in the selected range");

        var brain = _brains.FirstOrDefault(b => b.Name == config.BrainName) ?? _brains.FirstOrDefault();
        if (brain is null)
            throw new InvalidOperationException("no simulation brain registered");

        lock (_lock)
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            _ticks = ticks;
            _cursor = 0;
            _config = config;
            _traderSessions = CreateSessions(config.TraderCount, config.StartingCash);
            _aggregator = config.Aggregate ? new SimulationBarAggregator(config.BarSize) : null;
            _activeBrain = brain;
            _activeBrain.Reset();
            _recentBars.Clear();
            _recentTicks.Clear();
            _lastPrice = null;
            _lastTimestamp = null;
            _isPlaying = false;
            IsLoading = false;
        }
    }

    private static List<TraderSession> CreateSessions(int count, double startingCash) =>
        Enumerable.Range(1, Math.Max(1, count))
            .Select(i => new TraderSession { Id = i, Label = $"Trader {i}", Trader = new Trader.Trader(startingCash) })
            .ToList();

    public bool Play()
    {
        lock (_lock)
        {
            if (_ticks is null || _isPlaying || _cursor >= _ticks.Count) return false;
            _isPlaying = true;
        }
        _timer.Change(0, Timeout.Infinite);
        PublishSnapshot();
        return true;
    }

    public bool Pause()
    {
        lock (_lock)
        {
            if (!_isPlaying) return false;
            _isPlaying = false;
        }
        PublishSnapshot();
        return true;
    }

    public bool Step()
    {
        lock (_lock)
        {
            if (_isPlaying || _ticks is null || _cursor >= _ticks.Count) return false;
            AdvanceOneUnitLocked();
        }
        PublishSnapshot();
        return true;
    }

    public void SetSpeed(double multiplier)
    {
        lock (_lock) { _uncapped = false; _speedMultiplier = Math.Clamp(multiplier, 0.1, MaxFiniteMultiplier); }
        PublishSnapshot();
    }

    // Uncapped: TimerCallback drains every remaining tick in one callback instead
    // of a bounded slice per callback -- see the class-level doc comment.
    public void SetMaxSpeed()
    {
        lock (_lock) { _uncapped = true; }
        PublishSnapshot();
    }

    // Rewinds to the start of the same loaded config with a fresh Trader/Aggregator/Brain.
    public bool Reset()
    {
        lock (_lock)
        {
            if (_config is null) return false;
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            _cursor = 0;
            _traderSessions = CreateSessions(_config.TraderCount, _config.StartingCash);
            _aggregator = _config.Aggregate ? new SimulationBarAggregator(_config.BarSize) : null;
            _activeBrain?.Reset();
            _recentBars.Clear();
            _recentTicks.Clear();
            _lastPrice = null;
            _lastTimestamp = null;
            _isPlaying = false;
        }
        PublishSnapshot();
        return true;
    }

    // Back to the setup screen.
    public void Unload()
    {
        lock (_lock)
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            _ticks = null;
            _cursor = 0;
            _config = null;
            _traderSessions = new();
            _aggregator = null;
            _activeBrain = null;
            _isPlaying = false;
            _recentBars.Clear();
            _recentTicks.Clear();
            _lastPrice = null;
            _lastTimestamp = null;
        }
        Snapshot = null;
        Error = null;
        OnChanged?.Invoke();
    }

    private void TimerCallback(object? state)
    {
        bool reschedule;
        lock (_lock)
        {
            if (!_isPlaying || _ticks is null)
            {
                reschedule = false;
            }
            else
            {
                int units = _uncapped
                    ? _ticks.Count - _cursor
                    : Math.Clamp((int)Math.Round(BaseUnitsPerCallback * _speedMultiplier), 1, FiniteSpeedUnitsCap);
                for (int i = 0; i < units && _cursor < _ticks.Count; i++)
                    AdvanceOneUnitLocked();

                if (_cursor >= _ticks.Count) _isPlaying = false;
                reschedule = _isPlaying;
            }
        }
        PublishSnapshot();
        if (reschedule)
            _timer.Change(CallbackIntervalMs, Timeout.Infinite);
    }

    // Must be called while holding _lock.
    private void AdvanceOneUnitLocked()
    {
        var tick = _ticks![_cursor];
        _cursor++;

        double price = (tick.Bid + tick.Ask) / 2.0;
        _lastPrice = price;
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
                AppendBar(completed.Value);
                DecideAndApply(tick.Timestamp, completed.Value.Close, useBars: true);
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
                capital, session.Trader.CurrentPosition);

            var decision = _activeBrain!.Decide(observation);
            var detail = session.Trader.Apply(decision, price, timestamp);
            if (detail is not null)
                AppendLog(session, new SimulationLogEntry(timestamp, decision.Action.ToString(), price, detail));
        }
    }

    private void AppendBar(HlocAvdtBar bar)
    {
        _recentBars.Add(bar);
        if (_recentBars.Count > MaxRecentBars) _recentBars.RemoveAt(0);
    }

    private void AppendTick(Tick tick)
    {
        _recentTicks.Add(tick);
        if (_recentTicks.Count > MaxRecentTicks) _recentTicks.RemoveAt(0);
    }

    private void AppendLog(TraderSession session, SimulationLogEntry entry)
    {
        session.RecentLog.Add(entry);
        if (session.RecentLog.Count > MaxRecentLog) session.RecentLog.RemoveAt(0);
    }

    private void PublishSnapshot()
    {
        SimulationSnapshot? snapshot;
        lock (_lock)
        {
            snapshot = _config is null || _ticks is null
                ? null
                : new SimulationSnapshot(
                    _config, _isPlaying, _cursor >= _ticks.Count, _speedMultiplier, _uncapped,
                    _cursor, _ticks.Count, _lastTimestamp, _lastPrice,
                    _traderSessions.Select(ts => new TraderSnapshot(
                        ts.Id, ts.Label, ts.Trader.Snapshot(_lastPrice ?? 0), ts.Trader.CurrentPosition,
                        ts.Trader.TradeLog.Count, ts.RecentLog.ToList())).ToList(),
                    _recentBars.ToList(), _recentTicks.ToList());
        }
        Snapshot = snapshot;
        OnChanged?.Invoke();
    }

    public void Dispose() => _timer.Dispose();
}
