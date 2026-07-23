// ── Volume-profile feature engineering ───────────────────────────────────
//  Pure functions over BarPriceHistogram.Merge's output -- no brain-specific
//  state, so any brain (or future UI panel) can reuse them. Every offset is
//  expressed as (price - currentPrice) / currentPrice, matching the relative
//  normalization MlpPolicyBrain.Encode already uses for its price window, so
//  a network sees the same kind of bounded, scale-free number regardless of
//  the instrument's absolute price level.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Models;

internal readonly record struct VolumeProfileSummary(
    double PointOfControlOffset, double ValueAreaHighOffset, double ValueAreaLowOffset);

internal static class VolumeProfileAnalysis
{
    // Re-bins the merged buckets into a fixed-width histogram of volume
    // fraction-of-total, ascending by price -- always bucketCount entries
    // regardless of how many raw price levels or bars went into the merge,
    // so a network's input width never changes when the caller's lookback
    // window size changes. Empty/zero-volume input -> all zeros (neutral,
    // same "no false signal" convention as MlpPolicyBrain's zero-padding).
    public static float[] BuildRelativeHistogram(
        IReadOnlyList<(double Price, int Count, int UpCount, int DownCount)> buckets, int bucketCount)
    {
        var histogram = new float[bucketCount];
        if (buckets.Count == 0) return histogram;

        long totalCount = buckets.Sum(b => (long)b.Count);
        if (totalCount == 0) return histogram;

        double min = buckets.Min(b => b.Price), max = buckets.Max(b => b.Price);
        if (max <= min)
        {
            // Every tick landed in one price bucket (e.g. a near-empty lookback
            // window) -- no range to spread across, so park it all in the middle
            // bin rather than degenerating a divide-by-zero bucket width.
            histogram[bucketCount / 2] = 1f;
            return histogram;
        }

        double size = (max - min) / bucketCount;
        var counts = new long[bucketCount];
        foreach (var bucket in buckets)
        {
            int idx = Math.Clamp((int)((bucket.Price - min) / size), 0, bucketCount - 1);
            counts[idx] += bucket.Count;
        }

        for (int i = 0; i < bucketCount; i++)
            histogram[i] = (float)(counts[i] / (double)totalCount);
        return histogram;
    }

    // Standard Market-Profile value-area-expansion algorithm: start at the
    // max-volume bucket (point of control), then repeatedly grow the
    // [lowIdx, highIdx] range by whichever neighboring bucket (above or
    // below) carries more volume, until the included volume reaches
    // valueAreaFraction of the total (70% is the conventional default).
    // bucketWidth resolves Price (a bucket's low edge, per BarPriceHistogram)
    // back into POC/value-area price levels. Empty/zero-volume input -> all
    // zeros, same neutral convention as BuildRelativeHistogram.
    public static VolumeProfileSummary ComputeSummary(
        IReadOnlyList<(double Price, int Count, int UpCount, int DownCount)> buckets,
        double bucketWidth, double currentPrice, double valueAreaFraction = 0.70)
    {
        if (buckets.Count == 0 || currentPrice == 0) return default;

        var ordered = buckets.OrderBy(b => b.Price).ToList();
        long totalVolume = ordered.Sum(b => (long)b.Count);
        if (totalVolume == 0) return default;

        int pocIndex = 0;
        for (int i = 1; i < ordered.Count; i++)
            if (ordered[i].Count > ordered[pocIndex].Count) pocIndex = i;

        int lowIdx = pocIndex, highIdx = pocIndex;
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

        double pocPrice = ordered[pocIndex].Price + bucketWidth / 2.0;
        double valueAreaHigh = ordered[highIdx].Price + bucketWidth;
        double valueAreaLow = ordered[lowIdx].Price;

        return new VolumeProfileSummary(
            (pocPrice - currentPrice) / currentPrice,
            (valueAreaHigh - currentPrice) / currentPrice,
            (valueAreaLow - currentPrice) / currentPrice);
    }
}
