using System;
using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Helpers;

internal readonly record struct RangeStatsResult(
    double? Poc,
    double? Vah,
    double? Val,
    double Vwap,
    double Average,
    double Median,
    double Regression,
    double Max,
    double Min,
    int Count
);

internal readonly record struct ValueAreaResult(double Poc, double Vah, double Val);

internal readonly record struct PriceBucket(double Low, double High, double Weight);

// Same volume-profile bucketing VolumeProfileStats.Compute uses, generalized with a weight selector
// so it produces a true VWAP for volume-bearing items (e.g. bars) and a plain mean for unweighted ones (e.g. ticks).
internal static class RangeStatsHelper
{
    private const int BucketCount = 50;
    private const double ValueAreaFraction = 0.70;
    private const int MinSamplesPerBucket = 3;

    public static RangeStatsResult? Compute<T>(
        IReadOnlyList<T> items,
        Func<T, double> priceSelector,
        Func<T, double>? weightSelector = null
    )
    {
        if (items.Count == 0)
            return null;

        var prices = new double[items.Count];
        var weights = new double[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            prices[i] = priceSelector(items[i]);
            weights[i] = weightSelector?.Invoke(items[i]) ?? 1.0;
        }

        var totalWeight = weights.Sum();
        var (poc, vah, val) = ComputeVolumeProfile(prices, weights, totalWeight);

        return new RangeStatsResult(
            poc,
            vah,
            val,
            ComputeWeightedMean(prices, weights, totalWeight),
            prices.Average(),
            ComputeWeightedMedian(prices, weights, totalWeight),
            ComputeRegressionAtLastIndex(prices),
            prices.Max(),
            prices.Min(),
            items.Count
        );
    }

    // Splits raw samples into bucketCount uniform buckets across their observed price range -- the
    // histogram step charts render directly, shared with the bucketing Compute uses for its own POC/VAH/VAL.
    public static IReadOnlyList<PriceBucket> BucketSamples<T>(
        IReadOnlyList<T> items,
        Func<T, double> priceSelector,
        int bucketCount,
        Func<T, double>? weightSelector = null
    )
    {
        if (items.Count == 0 || bucketCount <= 0)
            return Array.Empty<PriceBucket>();

        var prices = new double[items.Count];
        var weights = new double[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            prices[i] = priceSelector(items[i]);
            weights[i] = weightSelector?.Invoke(items[i]) ?? 1.0;
        }

        var min = prices.Min();
        var max = prices.Max();
        if (max <= min)
        {
            var degenerate = new PriceBucket[bucketCount];
            degenerate[0] = new PriceBucket(min, min, weights.Sum());
            for (var i = 1; i < bucketCount; i++)
                degenerate[i] = new PriceBucket(min, min, 0);
            return degenerate;
        }

        var size = (max - min) / bucketCount;
        var bucketWeights = BucketWeights(prices, weights, min, size, bucketCount);

        var buckets = new PriceBucket[bucketCount];
        for (var i = 0; i < bucketCount; i++)
            buckets[i] = new PriceBucket(min + size * i, min + size * (i + 1), bucketWeights[i]);
        return buckets;
    }

    // For callers that already hold a fixed-width bucket histogram (e.g. BarPriceHistogram.Merge) instead
    // of raw samples -- same POC/widen-to-value-area algorithm as Compute, applied directly to their buckets.
    public static ValueAreaResult? ComputeValueArea<T>(
        IReadOnlyList<T> buckets,
        Func<T, double> priceSelector,
        Func<T, double> weightSelector,
        double bucketWidth,
        double valueAreaFraction = ValueAreaFraction,
        bool roundTargetUp = true
    )
    {
        if (buckets.Count == 0)
            return null;

        var ordered = buckets.OrderBy(priceSelector).ToList();
        var weights = ordered.Select(weightSelector).ToArray();
        var totalWeight = weights.Sum();
        if (totalWeight <= 0)
            return null;

        // Ceiling suits integer counts (a fractional target would let a whole extra bucket's
        // worth slip under the bar); continuous weights like dwell seconds want the raw fraction.
        var targetWeight = roundTargetUp ? Math.Ceiling(totalWeight * valueAreaFraction) : totalWeight * valueAreaFraction;
        var (pocIndex, lowIndex, highIndex) = WidenValueArea(weights, targetWeight);

        var poc = priceSelector(ordered[pocIndex]) + bucketWidth / 2.0;
        var vah = priceSelector(ordered[highIndex]) + bucketWidth;
        var val = priceSelector(ordered[lowIndex]);
        return new ValueAreaResult(poc, vah, val);
    }

    private static (double? Poc, double? Vah, double? Val) ComputeVolumeProfile(double[] prices, double[] weights, double totalWeight)
    {
        if (totalWeight < BucketCount * MinSamplesPerBucket)
            return (null, null, null);

        var min = prices.Min();
        var max = prices.Max();
        if (max <= min)
            return (min, min, min);

        var size = (max - min) / BucketCount;
        var bucketWeights = BucketWeights(prices, weights, min, size, BucketCount);

        var targetWeight = totalWeight * ValueAreaFraction;
        var (pocIndex, lowIndex, highIndex) = WidenValueArea(bucketWeights, targetWeight);

        double poc = min + size * (pocIndex + 0.5);
        double vah = min + size * (highIndex + 1);
        double val = min + size * lowIndex;
        return (poc, vah, val);
    }

    private static double[] BucketWeights(double[] prices, double[] weights, double min, double size, int bucketCount)
    {
        var bucketWeights = new double[bucketCount];
        for (var i = 0; i < prices.Length; i++)
        {
            var bucket = Math.Clamp((int)((prices[i] - min) / size), 0, bucketCount - 1);
            bucketWeights[bucket] += weights[i];
        }
        return bucketWeights;
    }

    // For callers bucketing several weight channels (e.g. total/up/down counts) over the same price
    // range -- one call per channel, sharing the caller's own min/bucketWidth so bounds stay in sync.
    public static double[] BucketWeights<T>(
        IReadOnlyList<T> items,
        Func<T, double> priceSelector,
        Func<T, double> weightSelector,
        double min,
        double bucketWidth,
        int bucketCount
    )
    {
        var bucketWeights = new double[bucketCount];
        foreach (var item in items)
        {
            var bucket = Math.Clamp((int)((priceSelector(item) - min) / bucketWidth), 0, bucketCount - 1);
            bucketWeights[bucket] += weightSelector(item);
        }
        return bucketWeights;
    }

    // Finds the highest-weight bucket, then greedily widens outward (favoring the heavier side on ties)
    // until targetWeight is covered or both edges are reached.
    internal static (int PocIndex, int LowIndex, int HighIndex) WidenValueArea(double[] bucketWeights, double targetWeight)
    {
        var pocIndex = 0;
        for (var i = 1; i < bucketWeights.Length; i++)
            if (bucketWeights[i] > bucketWeights[pocIndex])
                pocIndex = i;

        var coveredWeight = bucketWeights[pocIndex];
        var lowIndex = pocIndex;
        var highIndex = pocIndex;

        while (coveredWeight < targetWeight && (lowIndex > 0 || highIndex < bucketWeights.Length - 1))
        {
            var nextLowWeight = lowIndex > 0 ? bucketWeights[lowIndex - 1] : -1;
            var nextHighWeight = highIndex < bucketWeights.Length - 1 ? bucketWeights[highIndex + 1] : -1;

            if (nextHighWeight >= nextLowWeight)
            {
                highIndex++;
                coveredWeight += nextHighWeight;
            }
            else
            {
                lowIndex--;
                coveredWeight += nextLowWeight;
            }
        }

        return (pocIndex, lowIndex, highIndex);
    }

    private static double ComputeWeightedMean(double[] prices, double[] weights, double totalWeight)
    {
        if (totalWeight <= 0)
            return prices.Average();

        var sum = 0.0;
        for (var i = 0; i < prices.Length; i++)
            sum += prices[i] * weights[i];
        return sum / totalWeight;
    }

    private static double ComputeWeightedMedian(double[] prices, double[] weights, double totalWeight)
    {
        if (totalWeight <= 0)
            return prices[prices.Length / 2];

        var ordered = prices.Zip(weights, (price, weight) => (price, weight)).OrderBy(sample => sample.price).ToList();

        var half = totalWeight / 2.0;
        var cumulative = 0.0;
        foreach (var sample in ordered)
        {
            cumulative += sample.weight;
            if (cumulative >= half)
                return sample.price;
        }

        return ordered[^1].price;
    }

    // OLS fit of price against index, evaluated at the most recent (last) index.
    private static double ComputeRegressionAtLastIndex(double[] prices)
    {
        var n = prices.Length;
        if (n == 1)
            return prices[0];

        double sumX = 0,
            sumY = 0,
            sumXY = 0,
            sumXX = 0;
        for (var i = 0; i < n; i++)
        {
            sumX += i;
            sumY += prices[i];
            sumXY += i * prices[i];
            sumXX += (double)i * i;
        }

        var denominator = n * sumXX - sumX * sumX;
        if (denominator == 0)
            return prices.Average();

        var slope = (n * sumXY - sumX * sumY) / denominator;
        var intercept = (sumY - slope * sumX) / n;
        return slope * (n - 1) + intercept;
    }
}
