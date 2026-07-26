// ── Volume-profile feature engineering ───────────────────────────────────
//  Pure functions over BarPriceHistogram.Merge's output -- no brain-specific
//  state, so any brain (or future UI panel) can reuse them. ComputeSummary
//  returns raw POC/value-area prices, not normalized offsets -- turning a
//  raw price into a bounded, scale-free network input is
//  PriceOffsetNormalizer's job (see IFeatureNormalizer), not this analysis
//  layer's.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Models;

internal readonly record struct VolumeProfileSummary(
    double PointOfControl, double ValueAreaHigh, double ValueAreaLow);

internal static class VolumeProfileAnalysis
{
    // Shared re-binning step behind both BuildRelativeHistogram and
    // BuildRawHistogram below -- always bucketCount entries regardless of how
    // many raw price levels or bars went into the merge, so a network's
    // input width never changes when the caller's lookback window size
    // changes. Returns null (rather than an all-zero array) for empty/zero-
    // volume input so each caller applies its own neutral convention.
    // binMin/binSize are the rebin's own bucket edges (distinct from the
    // underlying BarPriceHistogram's fine-grained BucketWidth) -- callers that
    // need to know *where* a bucket sits, not just how much is in it (see
    // BuildRawHistogram), resolve bucket i's price as binMin + (i + 0.5) *
    // binSize.
    private static long[]? RebinCounts(
        IReadOnlyList<(double Price, int Count, int UpCount, int DownCount)> buckets, int bucketCount,
        out long totalCount, out double binMin, out double binSize)
    {
        totalCount = 0;
        binMin = 0;
        binSize = 0;
        if (buckets.Count == 0) return null;

        totalCount = buckets.Sum(b => (long)b.Count);
        if (totalCount == 0) return null;

        var counts = new long[bucketCount];
        double min = buckets.Min(b => b.Price), max = buckets.Max(b => b.Price);
        binMin = min;
        if (max <= min)
        {
            // Every tick landed in one price bucket (e.g. a near-empty lookback
            // window) -- no range to spread across, so park it all in the middle
            // bin rather than degenerating a divide-by-zero bucket width;
            // binSize stays 0, which correctly resolves every bucket's price
            // to binMin since there's genuinely only one price level here.
            counts[bucketCount / 2] = totalCount;
            return counts;
        }

        binSize = (max - min) / bucketCount;
        foreach (var bucket in buckets)
        {
            int idx = Math.Clamp((int)((bucket.Price - min) / binSize), 0, bucketCount - 1);
            counts[idx] += bucket.Count;
        }
        return counts;
    }

    // Re-bins the merged buckets into a fixed-width histogram of volume
    // fraction-of-total, ascending by price. Empty/zero-volume input -> all
    // zeros (neutral, same "no false signal" convention as MlpPolicyBrain's
    // zero-padding).
    public static float[] BuildRelativeHistogram(
        IReadOnlyList<(double Price, int Count, int UpCount, int DownCount)> buckets, int bucketCount)
    {
        var histogram = new float[bucketCount];
        var counts = RebinCounts(buckets, bucketCount, out var totalCount, out _, out _);
        if (counts is null) return histogram;

        for (int i = 0; i < bucketCount; i++)
            histogram[i] = (float)(counts[i] / (double)totalCount);
        return histogram;
    }

    // Raw-value counterpart to BuildRelativeHistogram -- same fixed-width
    // re-binning, but returns each bucket's literal tick count paired with the
    // exact price level that bucket represents (its center), for a caller
    // (VolumeProfileRawEncoder) that wants the network fed exact activity
    // *and* exactly where it happened. A bucket's price is not recoverable
    // from its index alone -- the rebin's [min, max] range shifts with
    // whatever the lookback window actually spanned, so bucket i can sit at a
    // different real price every decision; without the price alongside the
    // count, a network has no way to tell which bucket the current price
    // (SimulationObservation.CurrentPrice, via TraderStateEncoder) falls
    // into. Empty/zero-volume input -> all-zero counts and prices, same
    // neutral convention as BuildRelativeHistogram.
    public static (float[] Counts, float[] Prices) BuildRawHistogram(
        IReadOnlyList<(double Price, int Count, int UpCount, int DownCount)> buckets, int bucketCount)
    {
        var countsOut = new float[bucketCount];
        var pricesOut = new float[bucketCount];
        var counts = RebinCounts(buckets, bucketCount, out _, out var binMin, out var binSize);
        if (counts is null) return (countsOut, pricesOut);

        for (int i = 0; i < bucketCount; i++)
        {
            countsOut[i] = counts[i];
            pricesOut[i] = (float)(binMin + (i + 0.5) * binSize);
        }
        return (countsOut, pricesOut);
    }

    // Standard Market-Profile value-area-expansion algorithm: start at the
    // max-volume bucket (point of control), then repeatedly grow the
    // [lowIdx, highIdx] range by whichever neighboring bucket (above or
    // below) carries more volume, until the included volume reaches
    // valueAreaFraction of the total (70% is the conventional default).
    // bucketWidth resolves Price (a bucket's low edge, per BarPriceHistogram)
    // back into POC/value-area price levels. Empty/zero-volume input -> null
    // -- an actual raw price of 0 is not a meaningful "no data" stand-in the
    // way an offset of 0 was, so the caller (VolumeProfileEncoder) has to
    // decide the neutral fallback itself rather than normalizing a fake 0
    // price.
    public static VolumeProfileSummary? ComputeSummary(
        IReadOnlyList<(double Price, int Count, int UpCount, int DownCount)> buckets,
        double bucketWidth, double valueAreaFraction = 0.70)
    {
        if (buckets.Count == 0) return null;

        var ordered = buckets.OrderBy(b => b.Price).ToList();
        long totalVolume = ordered.Sum(b => (long)b.Count);
        if (totalVolume == 0) return null;

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

        return new VolumeProfileSummary(
            ordered[pocIndex].Price + bucketWidth / 2.0,
            ordered[highIdx].Price + bucketWidth,
            ordered[lowIdx].Price);
    }
}
