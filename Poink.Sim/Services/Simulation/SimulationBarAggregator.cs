
using System;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Simulation;

internal sealed class SimulationBarAggregator
{
    private readonly TimeSpan _barSize;
    private readonly double _bucketWidth;

    private DateTime? _barStart;
    private DateTime _closeTime, _highTime, _lowTime;
    private double _open, _high, _low, _close, _sum, _distance, _prevMid;
    private int _count, _upVolume, _downVolume;
    private BarPriceHistogram _histogram;

    public SimulationBarAggregator(TimeSpan barSize, double bucketWidth)
    {
        _barSize = barSize;
        _bucketWidth = bucketWidth;
        _histogram = new BarPriceHistogram(bucketWidth);
    }

    public HlocAvdtBar? CurrentPartialBar =>
        _barStart is null || _count == 0
            ? null
            : new HlocAvdtBar(_barStart.Value, _closeTime, _highTime, _lowTime,
                _open, _high, _low, _close, _sum / _count, _count, _distance, _upVolume, _downVolume);


    public BarPriceHistogram? CurrentPartialHistogram =>
        _barStart is null || _count == 0 ? null : _histogram;


    public (HlocAvdtBar Bar, BarPriceHistogram Histogram)? Feed(Tick tick)
    {
        double mid = (tick.Bid + tick.Ask) / 2.0;
        var thisBarStart = new DateTime(
            tick.Timestamp.Ticks - (tick.Timestamp.Ticks % _barSize.Ticks), tick.Timestamp.Kind);

        (HlocAvdtBar, BarPriceHistogram)? completed = null;

        if (_barStart is null || thisBarStart != _barStart)
        {
            if (_barStart is not null && _count > 0)
                completed = (new HlocAvdtBar(_barStart.Value, _closeTime, _highTime, _lowTime,
                    _open, _high, _low, _close, _sum / _count, _count, _distance, _upVolume, _downVolume), _histogram);

            _barStart = thisBarStart;
            _open = _high = _low = _close = _sum = mid;
            _closeTime = _highTime = _lowTime = tick.Timestamp;
            _prevMid = mid;
            _distance = 0;
            _count = 1;
            _upVolume = 0;
            _downVolume = 0;
            _histogram = new BarPriceHistogram(_bucketWidth);
            _histogram.Add(mid, 0);
        }
        else
        {
            int direction = mid > _prevMid ? 1 : mid < _prevMid ? -1 : 0;
            if (mid > _high)
            { _high = mid; _highTime = tick.Timestamp; }
            if (mid < _low)
            { _low = mid; _lowTime = tick.Timestamp; }
            if (direction > 0)
                _upVolume++;
            else if (direction < 0)
                _downVolume++;
            _close = mid;
            _closeTime = tick.Timestamp;
            _sum += mid;
            _distance += Math.Abs(mid - _prevMid);
            _prevMid = mid;
            _count++;
            _histogram.Add(mid, direction);
        }

        return completed;
    }

    public void Reset()
    {
        _barStart = null;
        _closeTime = _highTime = _lowTime = default;
        _open = _high = _low = _close = _sum = _distance = _prevMid = 0;
        _count = _upVolume = _downVolume = 0;
        _histogram = new BarPriceHistogram(_bucketWidth);
    }
}
