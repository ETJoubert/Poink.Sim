// ── Incremental tick -> HLOC/AVDT bar aggregation ────────────────────────
//  Feature-specific aggregator for Simulation: unlike Poink.ML's batch,
//  static, whole-list-up-front aggregators, a sim advances tick-by-tick as
//  the user plays/steps forward, so this is a stateful class that holds one
//  in-progress bar's accumulators between calls and exposes "feed one tick,
//  get zero-or-one completed bar back." The per-tick math (bar-boundary
//  snap, Distance accumulation, tick-rule Up/DownVolume) mirrors Poink.ML's
//  HlocAvdtAggregator.Aggregate exactly, just evaluated one tick at a time
//  instead of over the whole list.
// ─────────────────────────────────────────────────────────────────────────
using System;
using Poink.Sim.Repositories;

namespace Poink.Sim.Features.Simulation;

internal sealed class SimulationBarAggregator
{
    private readonly TimeSpan _barSize;

    private DateTime? _barStart;
    private DateTime _closeTime, _highTime, _lowTime;
    private double _open, _high, _low, _close, _sum, _distance, _prevMid;
    private int _count, _upVolume, _downVolume;

    public SimulationBarAggregator(TimeSpan barSize) => _barSize = barSize;

    // The in-progress (not yet closed) bar, for a live "forming bar" chart
    // candle without waiting for it to close. Null before the first Feed() call.
    public HlocAvdtBar? CurrentPartialBar =>
        _barStart is null || _count == 0
            ? null
            : new HlocAvdtBar(_barStart.Value, _closeTime, _highTime, _lowTime,
                _open, _high, _low, _close, _sum / _count, _count, _distance, _upVolume, _downVolume);

    // Feeds one tick. Returns the just-completed bar if this tick started a new
    // bar boundary (the prior bar flushes); otherwise null (the current bar is
    // still open -- inspect CurrentPartialBar if a live candle is needed).
    public HlocAvdtBar? Feed(Tick tick)
    {
        double mid = (tick.Bid + tick.Ask) / 2.0;
        var thisBarStart = new DateTime(
            tick.Timestamp.Ticks - (tick.Timestamp.Ticks % _barSize.Ticks), tick.Timestamp.Kind);

        HlocAvdtBar? completed = null;

        if (_barStart is null || thisBarStart != _barStart)
        {
            if (_barStart is not null && _count > 0)
                completed = new HlocAvdtBar(_barStart.Value, _closeTime, _highTime, _lowTime,
                    _open, _high, _low, _close, _sum / _count, _count, _distance, _upVolume, _downVolume);

            _barStart = thisBarStart;
            _open = _high = _low = _close = _sum = mid;
            _closeTime = _highTime = _lowTime = tick.Timestamp;
            _prevMid = mid;
            _distance = 0;
            _count = 1;
            _upVolume = 0;
            _downVolume = 0;
        }
        else
        {
            if (mid > _high) { _high = mid; _highTime = tick.Timestamp; }
            if (mid < _low) { _low = mid; _lowTime = tick.Timestamp; }
            if (mid > _prevMid) _upVolume++;
            else if (mid < _prevMid) _downVolume++;
            _close = mid;
            _closeTime = tick.Timestamp;
            _sum += mid;
            _distance += Math.Abs(mid - _prevMid);
            _prevMid = mid;
            _count++;
        }

        return completed;
    }

    public void Reset()
    {
        _barStart = null;
        _closeTime = _highTime = _lowTime = default;
        _open = _high = _low = _close = _sum = _distance = _prevMid = 0;
        _count = _upVolume = _downVolume = 0;
    }
}
