using System;
using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Models;

internal readonly record struct VolumeProfileSummary(double PointOfControl, double ValueAreaHigh, double ValueAreaLow);

internal static class VolumeProfileAnalysis
{
    private static (long[] Counts, long[] UpCounts, long[] DownCounts)? RebinSplit(
        IReadOnlyList<(double Price, int Count, int UpCount, int DownCount)> buckets,
        int bucketCount,
        out long totalCount,
        out double binMin,
        out double binSize
    )
    {
        totalCount = 0;
        binMin = 0;
        binSize = 0;
        if (buckets.Count == 0)
            return null;

        totalCount = buckets.Sum(b => (long)b.Count);
        if (totalCount == 0)
            return null;

        var counts = new long[bucketCount];
        var upCounts = new long[bucketCount];
        var downCounts = new long[bucketCount];
        double min = buckets.Min(b => b.Price),
            max = buckets.Max(b => b.Price);
        binMin = min;
        if (max <= min)
        {
            int mid = bucketCount / 2;
            counts[mid] = totalCount;
            upCounts[mid] = buckets.Sum(b => (long)b.UpCount);
            downCounts[mid] = buckets.Sum(b => (long)b.DownCount);
            return (counts, upCounts, downCounts);
        }

        binSize = (max - min) / bucketCount;
        foreach (var bucket in buckets)
        {
            int idx = Math.Clamp((int)((bucket.Price - min) / binSize), 0, bucketCount - 1);
            counts[idx] += bucket.Count;
            upCounts[idx] += bucket.UpCount;
            downCounts[idx] += bucket.DownCount;
        }
        return (counts, upCounts, downCounts);
    }

    private static long[]? RebinCounts(
        IReadOnlyList<(double Price, int Count, int UpCount, int DownCount)> buckets,
        int bucketCount,
        out long totalCount,
        out double binMin,
        out double binSize
    ) => RebinSplit(buckets, bucketCount, out totalCount, out binMin, out binSize)?.Counts;

    public static float[] BuildRelativeHistogram(
        IReadOnlyList<(double Price, int Count, int UpCount, int DownCount)> buckets,
        int bucketCount
    )
    {
        var histogram = new float[bucketCount];
        var counts = RebinCounts(buckets, bucketCount, out var totalCount, out _, out _);
        if (counts is null)
            return histogram;

        for (int i = 0; i < bucketCount; i++)
            histogram[i] = (float)(counts[i] / (double)totalCount);
        return histogram;
    }

    public static (float[] Counts, float[] Prices) BuildRawHistogram(
        IReadOnlyList<(double Price, int Count, int UpCount, int DownCount)> buckets,
        int bucketCount
    )
    {
        var countsOut = new float[bucketCount];
        var pricesOut = new float[bucketCount];
        var counts = RebinCounts(buckets, bucketCount, out _, out var binMin, out var binSize);
        if (counts is null)
            return (countsOut, pricesOut);

        for (int i = 0; i < bucketCount; i++)
        {
            countsOut[i] = counts[i];
            pricesOut[i] = (float)(binMin + (i + 0.5) * binSize);
        }
        return (countsOut, pricesOut);
    }

    public static (float[] Up, float[] Down, float[] Diff) BuildRelativeSplitHistogram(
        IReadOnlyList<(double Price, int Count, int UpCount, int DownCount)> buckets,
        int bucketCount
    )
    {
        var up = new float[bucketCount];
        var down = new float[bucketCount];
        var diff = new float[bucketCount];
        if (RebinSplit(buckets, bucketCount, out var totalCount, out _, out _) is not { } split)
            return (up, down, diff);

        for (int i = 0; i < bucketCount; i++)
        {
            up[i] = (float)(split.UpCounts[i] / (double)totalCount);
            down[i] = (float)(split.DownCounts[i] / (double)totalCount);
            diff[i] = up[i] - down[i];
        }
        return (up, down, diff);
    }

    public static (float[] Up, float[] Down, float[] Diff, float[] Prices) BuildRawSplitHistogram(
        IReadOnlyList<(double Price, int Count, int UpCount, int DownCount)> buckets,
        int bucketCount
    )
    {
        var up = new float[bucketCount];
        var down = new float[bucketCount];
        var diff = new float[bucketCount];
        var prices = new float[bucketCount];
        if (RebinSplit(buckets, bucketCount, out _, out var binMin, out var binSize) is not { } split)
            return (up, down, diff, prices);

        for (int i = 0; i < bucketCount; i++)
        {
            up[i] = split.UpCounts[i];
            down[i] = split.DownCounts[i];
            diff[i] = up[i] - down[i];
            prices[i] = (float)(binMin + (i + 0.5) * binSize);
        }
        return (up, down, diff, prices);
    }

    public static VolumeProfileSummary? ComputeSummary(
        IReadOnlyList<(double Price, int Count, int UpCount, int DownCount)> buckets,
        double bucketWidth,
        double valueAreaFraction = 0.70
    )
    {
        if (buckets.Count == 0)
            return null;

        var ordered = buckets.OrderBy(b => b.Price).ToList();
        long totalVolume = ordered.Sum(b => (long)b.Count);
        if (totalVolume == 0)
            return null;

        int pocIndex = 0;
        for (int i = 1; i < ordered.Count; i++)
            if (ordered[i].Count > ordered[pocIndex].Count)
                pocIndex = i;

        int lowIdx = pocIndex,
            highIdx = pocIndex;
        long includedVolume = ordered[pocIndex].Count;
        long target = (long)Math.Ceiling(totalVolume * valueAreaFraction);
        while (includedVolume < target && (lowIdx > 0 || highIdx < ordered.Count - 1))
        {
            long belowVolume = lowIdx > 0 ? ordered[lowIdx - 1].Count : -1;
            long aboveVolume = highIdx < ordered.Count - 1 ? ordered[highIdx + 1].Count : -1;
            if (aboveVolume >= belowVolume)
            {
                highIdx++;
                includedVolume += ordered[highIdx].Count;
            }
            else
            {
                lowIdx--;
                includedVolume += ordered[lowIdx].Count;
            }
        }

        return new VolumeProfileSummary(
            ordered[pocIndex].Price + bucketWidth / 2.0,
            ordered[highIdx].Price + bucketWidth,
            ordered[lowIdx].Price
        );
    }
}

