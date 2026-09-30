using System;
using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Helpers;

internal readonly record struct TimeProfileLevels(double Poc, double ValueAreaHigh, double ValueAreaLow);

internal readonly record struct TimeProfileSummary(double PointOfControl, double ValueAreaHigh, double ValueAreaLow);

// Dwell-time-weighted counterpart to RangeStatsHelper/VolumeProfileAnalysis: POC/value-area and
// feature histograms weighted by seconds spent at a price rather than tick/volume count.
internal static class TimeProfileHelper
{
    private const int BucketCount = 50;
    private const int MinSamplesPerBucket = 3;
    private const double ValueAreaFraction = 0.70;

    // Gated on sample count (not total seconds) so a fast-ticking window with little elapsed
    // time still counts as "enough data"; totalSeconds is checked separately below.
    public static TimeProfileLevels? Compute<T>(
        IReadOnlyList<T> samples,
        Func<T, double> priceSelector,
        Func<T, double> countSelector,
        Func<T, double> secondsSelector
    )
    {
        var totalCount = samples.Sum(countSelector);
        if (totalCount < BucketCount * MinSamplesPerBucket)
            return null;

        var buckets = RangeStatsHelper.BucketSamples(samples, priceSelector, BucketCount, secondsSelector);
        if (buckets[0].Low == buckets[0].High)
        {
            var single = buckets[0].Low;
            return new TimeProfileLevels(single, single, single);
        }

        var bucketWeights = buckets.Select(b => b.Weight).ToArray();
        var totalSeconds = bucketWeights.Sum();
        // Enough samples but no elapsed time between them yet (e.g. right after Load/Reset).
        if (totalSeconds <= 0)
            return null;

        var (pocIndex, lowIndex, highIndex) = RangeStatsHelper.WidenValueArea(bucketWeights, totalSeconds * ValueAreaFraction);
        return new TimeProfileLevels((buckets[pocIndex].Low + buckets[pocIndex].High) / 2, buckets[highIndex].High, buckets[lowIndex].Low);
    }

    public static TimeProfileSummary? ComputeSummary(
        IReadOnlyList<(double Price, int Count, double Seconds, double UpSeconds, double DownSeconds)> buckets,
        double bucketWidth,
        double valueAreaFraction = ValueAreaFraction
    ) =>
        RangeStatsHelper.ComputeValueArea(buckets, b => b.Price, b => b.Seconds, bucketWidth, valueAreaFraction, roundTargetUp: false) is { } area
            ? new TimeProfileSummary(area.Poc, area.Vah, area.Val)
            : null;

    private static (double[] Seconds, double[] UpSeconds, double[] DownSeconds)? RebinSecondsSplit(
        IReadOnlyList<(double Price, int Count, double Seconds, double UpSeconds, double DownSeconds)> buckets,
        int bucketCount,
        out double totalSeconds,
        out double binMin,
        out double binSize
    )
    {
        totalSeconds = 0;
        binMin = 0;
        binSize = 0;
        if (buckets.Count == 0)
            return null;

        totalSeconds = buckets.Sum(b => b.Seconds);
        if (totalSeconds <= 0)
            return null;

        var seconds = new double[bucketCount];
        var upSeconds = new double[bucketCount];
        var downSeconds = new double[bucketCount];
        double min = buckets.Min(b => b.Price),
            max = buckets.Max(b => b.Price);
        binMin = min;
        if (max <= min)
        {
            int mid = bucketCount / 2;
            seconds[mid] = totalSeconds;
            upSeconds[mid] = buckets.Sum(b => b.UpSeconds);
            downSeconds[mid] = buckets.Sum(b => b.DownSeconds);
            return (seconds, upSeconds, downSeconds);
        }

        binSize = (max - min) / bucketCount;
        foreach (var bucket in buckets)
        {
            int idx = Math.Clamp((int)((bucket.Price - min) / binSize), 0, bucketCount - 1);
            seconds[idx] += bucket.Seconds;
            upSeconds[idx] += bucket.UpSeconds;
            downSeconds[idx] += bucket.DownSeconds;
        }
        return (seconds, upSeconds, downSeconds);
    }

    private static double[]? RebinSeconds(
        IReadOnlyList<(double Price, int Count, double Seconds, double UpSeconds, double DownSeconds)> buckets,
        int bucketCount,
        out double totalSeconds,
        out double binMin,
        out double binSize
    ) => RebinSecondsSplit(buckets, bucketCount, out totalSeconds, out binMin, out binSize)?.Seconds;

    public static float[] BuildRelativeHistogram(
        IReadOnlyList<(double Price, int Count, double Seconds, double UpSeconds, double DownSeconds)> buckets,
        int bucketCount
    )
    {
        var histogram = new float[bucketCount];
        var seconds = RebinSeconds(buckets, bucketCount, out var totalSeconds, out _, out _);
        if (seconds is null)
            return histogram;

        for (int i = 0; i < bucketCount; i++)
            histogram[i] = (float)(seconds[i] / totalSeconds);
        return histogram;
    }

    public static (float[] Seconds, float[] Prices) BuildRawHistogram(
        IReadOnlyList<(double Price, int Count, double Seconds, double UpSeconds, double DownSeconds)> buckets,
        int bucketCount
    )
    {
        var secondsOut = new float[bucketCount];
        var pricesOut = new float[bucketCount];
        var seconds = RebinSeconds(buckets, bucketCount, out _, out var binMin, out var binSize);
        if (seconds is null)
            return (secondsOut, pricesOut);

        for (int i = 0; i < bucketCount; i++)
        {
            secondsOut[i] = (float)seconds[i];
            pricesOut[i] = (float)(binMin + (i + 0.5) * binSize);
        }
        return (secondsOut, pricesOut);
    }

    public static (float[] Up, float[] Down, float[] Diff) BuildRelativeSplitHistogram(
        IReadOnlyList<(double Price, int Count, double Seconds, double UpSeconds, double DownSeconds)> buckets,
        int bucketCount
    )
    {
        var up = new float[bucketCount];
        var down = new float[bucketCount];
        var diff = new float[bucketCount];
        if (RebinSecondsSplit(buckets, bucketCount, out var totalSeconds, out _, out _) is not { } split)
            return (up, down, diff);

        for (int i = 0; i < bucketCount; i++)
        {
            up[i] = (float)(split.UpSeconds[i] / totalSeconds);
            down[i] = (float)(split.DownSeconds[i] / totalSeconds);
            diff[i] = up[i] - down[i];
        }
        return (up, down, diff);
    }

    public static (float[] Up, float[] Down, float[] Diff, float[] Prices) BuildRawSplitHistogram(
        IReadOnlyList<(double Price, int Count, double Seconds, double UpSeconds, double DownSeconds)> buckets,
        int bucketCount
    )
    {
        var up = new float[bucketCount];
        var down = new float[bucketCount];
        var diff = new float[bucketCount];
        var prices = new float[bucketCount];
        if (RebinSecondsSplit(buckets, bucketCount, out _, out var binMin, out var binSize) is not { } split)
            return (up, down, diff, prices);

        for (int i = 0; i < bucketCount; i++)
        {
            up[i] = (float)split.UpSeconds[i];
            down[i] = (float)split.DownSeconds[i];
            diff[i] = up[i] - down[i];
            prices[i] = (float)(binMin + (i + 0.5) * binSize);
        }
        return (up, down, diff, prices);
    }
}
