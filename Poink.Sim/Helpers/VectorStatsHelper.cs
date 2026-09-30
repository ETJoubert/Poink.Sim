using System;
using System.Collections.Generic;

namespace Poink.Sim.Helpers;

internal readonly record struct VectorPoint(DateTime TimeUtc, double Value);

// Running/rolling series measurements (one point per bar) -- the vector counterpart to RangeStatsHelper's scalars.
internal static class VectorStatsHelper
{
    public static List<VectorPoint> Vwap<T>(
        IEnumerable<T> bars,
        Func<T, DateTime> timeSelector,
        Func<T, double> priceSelector,
        Func<T, double> volumeSelector
    )
    {
        var points = new List<VectorPoint>();
        double cumulativePriceVolume = 0,
            cumulativeVolume = 0;
        foreach (var bar in bars)
        {
            var volume = volumeSelector(bar);
            cumulativePriceVolume += priceSelector(bar) * volume;
            cumulativeVolume += volume;
            if (cumulativeVolume > 0)
                points.Add(new VectorPoint(timeSelector(bar), cumulativePriceVolume / cumulativeVolume));
        }
        return points;
    }
}
