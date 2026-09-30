using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Helpers;

internal readonly record struct AggregatedBar(CTraderBarRow Bar, double TrueHigh, double TrueLow);

// Batch bar aggregation over an already-fetched tick list (vs. SimulationBarAggregator's streaming one).
// Quartile/Decile carry percentile bounds in Open/Close; MeanStdDev replaces High/Low with a mean+-stddev band.
internal static class BarAggregator
{
    public static AggregatedBar? Aggregate(IReadOnlyList<Tick> ticks, int startIndex, BarAggregationKind kind)
    {
        if (startIndex >= ticks.Count)
            return null;

        var openTime = ticks[startIndex].Timestamp;
        double open = 0,
            close = 0,
            prevMid = 0;
        var prices = new List<double>(ticks.Count - startIndex);
        int upVolume = 0,
            downVolume = 0;

        for (var i = startIndex; i < ticks.Count; i++)
        {
            var mid = (ticks[i].Bid + ticks[i].Ask) / 2.0;
            if (i == startIndex)
                open = mid;
            else if (mid > prevMid)
                upVolume++;
            else if (mid < prevMid)
                downVolume++;

            prevMid = mid;
            close = mid;
            prices.Add(mid);
        }

        prices.Sort();
        var mean = prices.Average();
        var trueHigh = prices[^1];
        var trueLow = prices[0];

        var bar = kind switch
        {
            BarAggregationKind.Quartile
                => new CTraderBarRow(openTime, Percentile(prices, 0.25), trueHigh, trueLow, Percentile(prices, 0.75), prices.Count),
            BarAggregationKind.Decile
                => new CTraderBarRow(openTime, Percentile(prices, 0.1), trueHigh, trueLow, Percentile(prices, 0.9), prices.Count),
            BarAggregationKind.MeanStdDev
                => new CTraderBarRow(openTime, mean, mean + StdDev(prices, mean), mean - StdDev(prices, mean), mean, prices.Count),
            _ => new CTraderBarRow(openTime, open, trueHigh, trueLow, close, prices.Count, upVolume, downVolume),
        };
        return new AggregatedBar(bar, trueHigh, trueLow);
    }

    // Linear interpolation between closest ranks -- matches Excel's PERCENTILE.INC and NumPy's default method.
    private static double Percentile(List<double> sortedValues, double fraction)
    {
        if (sortedValues.Count == 1)
            return sortedValues[0];

        var rank = fraction * (sortedValues.Count - 1);
        var lowerIndex = (int)Math.Floor(rank);
        var upperIndex = (int)Math.Ceiling(rank);
        if (lowerIndex == upperIndex)
            return sortedValues[lowerIndex];

        var weight = rank - lowerIndex;
        return sortedValues[lowerIndex] + (sortedValues[upperIndex] - sortedValues[lowerIndex]) * weight;
    }

    private static double StdDev(List<double> values, double mean) =>
        values.Count <= 1 ? 0 : Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Count);
}
