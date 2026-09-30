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
    double Regression
);

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
            ComputeRegressionAtLastIndex(prices)
        );
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
        var bucketWeights = new double[BucketCount];
        for (var i = 0; i < prices.Length; i++)
        {
            var bucket = Math.Clamp((int)((prices[i] - min) / size), 0, BucketCount - 1);
            bucketWeights[bucket] += weights[i];
        }

        var pocIndex = 0;
        for (var i = 1; i < BucketCount; i++)
            if (bucketWeights[i] > bucketWeights[pocIndex])
                pocIndex = i;

        var targetWeight = totalWeight * ValueAreaFraction;
        var coveredWeight = bucketWeights[pocIndex];
        var lowIndex = pocIndex;
        var highIndex = pocIndex;

        while (coveredWeight < targetWeight && (lowIndex > 0 || highIndex < BucketCount - 1))
        {
            var nextLowWeight = lowIndex > 0 ? bucketWeights[lowIndex - 1] : -1;
            var nextHighWeight = highIndex < BucketCount - 1 ? bucketWeights[highIndex + 1] : -1;

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

        double poc = min + size * (pocIndex + 0.5);
        double vah = min + size * (highIndex + 1);
        double val = min + size * lowIndex;
        return (poc, vah, val);
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
