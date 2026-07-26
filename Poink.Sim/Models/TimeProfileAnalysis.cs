// ── Time-profile feature engineering ─────────────────────────────────────
//  Time-weighted counterpart to VolumeProfileAnalysis, over BarPriceHistogram.
//  MergeTime's output instead of Merge's -- same relative-histogram/point-of-
//  control/value-area shape, but every bucket is weighted by seconds spent
//  there instead of tick count, so it tells apart "many ticks in an instant"
//  from "price parked here a while" the way volume profile alone can't.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Models;

internal readonly record struct TimeProfileSummary(
    double PointOfControl, double ValueAreaHigh, double ValueAreaLow);

internal static class TimeProfileAnalysis
{
    // Shared re-binning step behind both BuildRelativeHistogram and
    // BuildRawHistogram below -- same shape/edge-case handling as
    // VolumeProfileAnalysis.RebinCounts, weighted by Seconds instead of
    // Count. Returns null (rather than an all-zero array) for empty/zero-
    // time input so each caller applies its own neutral convention. binMin/
    // binSize are the rebin's own bucket edges -- see VolumeProfileAnalysis.
    // RebinCounts' doc comment.
    private static double[]? RebinSeconds(
        IReadOnlyList<(double Price, int Count, double Seconds, double UpSeconds, double DownSeconds)> buckets, int bucketCount,
        out double totalSeconds, out double binMin, out double binSize)
    {
        totalSeconds = 0;
        binMin = 0;
        binSize = 0;
        if (buckets.Count == 0) return null;

        totalSeconds = buckets.Sum(b => b.Seconds);
        if (totalSeconds <= 0) return null;

        var seconds = new double[bucketCount];
        double min = buckets.Min(b => b.Price), max = buckets.Max(b => b.Price);
        binMin = min;
        if (max <= min)
        {
            // Every sample landed in one price bucket -- no range to spread
            // across, park it all in the middle bin (same convention as
            // VolumeProfileAnalysis).
            seconds[bucketCount / 2] = totalSeconds;
            return seconds;
        }

        binSize = (max - min) / bucketCount;
        foreach (var bucket in buckets)
        {
            int idx = Math.Clamp((int)((bucket.Price - min) / binSize), 0, bucketCount - 1);
            seconds[idx] += bucket.Seconds;
        }
        return seconds;
    }

    // Re-bins the merged buckets into a fixed-width histogram of seconds
    // fraction-of-total, ascending by price. Empty/zero-time input -> all
    // zeros (neutral).
    public static float[] BuildRelativeHistogram(
        IReadOnlyList<(double Price, int Count, double Seconds, double UpSeconds, double DownSeconds)> buckets, int bucketCount)
    {
        var histogram = new float[bucketCount];
        var seconds = RebinSeconds(buckets, bucketCount, out var totalSeconds, out _, out _);
        if (seconds is null) return histogram;

        for (int i = 0; i < bucketCount; i++)
            histogram[i] = (float)(seconds[i] / totalSeconds);
        return histogram;
    }

    // Raw-value counterpart to BuildRelativeHistogram -- same fixed-width
    // re-binning, but returns each bucket's literal seconds-spent paired with
    // the exact price level that bucket represents (its center), for the same
    // reason as VolumeProfileAnalysis.BuildRawHistogram: a bucket's price
    // isn't recoverable from its index alone, and a network needs it to
    // locate the current price (via TraderStateEncoder) against this
    // distribution. Empty/zero-time input -> all-zero seconds and prices.
    public static (float[] Seconds, float[] Prices) BuildRawHistogram(
        IReadOnlyList<(double Price, int Count, double Seconds, double UpSeconds, double DownSeconds)> buckets, int bucketCount)
    {
        var secondsOut = new float[bucketCount];
        var pricesOut = new float[bucketCount];
        var seconds = RebinSeconds(buckets, bucketCount, out _, out var binMin, out var binSize);
        if (seconds is null) return (secondsOut, pricesOut);

        for (int i = 0; i < bucketCount; i++)
        {
            secondsOut[i] = (float)seconds[i];
            pricesOut[i] = (float)(binMin + (i + 0.5) * binSize);
        }
        return (secondsOut, pricesOut);
    }

    // Same value-area-expansion algorithm as VolumeProfileAnalysis.ComputeSummary,
    // ranked by seconds spent at a price level instead of tick count -- the
    // Point of Control is the price the market dwelled at longest, not
    // necessarily the one with the most ticks. Empty/zero-time input -> null,
    // same reasoning as VolumeProfileAnalysis.ComputeSummary: a raw price of 0
    // isn't a meaningful "no data" stand-in the way an offset of 0 was.
    public static TimeProfileSummary? ComputeSummary(
        IReadOnlyList<(double Price, int Count, double Seconds, double UpSeconds, double DownSeconds)> buckets,
        double bucketWidth, double valueAreaFraction = 0.70)
    {
        if (buckets.Count == 0) return null;

        var ordered = buckets.OrderBy(b => b.Price).ToList();
        double totalSeconds = ordered.Sum(b => b.Seconds);
        if (totalSeconds <= 0) return null;

        int pocIndex = 0;
        for (int i = 1; i < ordered.Count; i++)
            if (ordered[i].Seconds > ordered[pocIndex].Seconds) pocIndex = i;

        int lowIdx = pocIndex, highIdx = pocIndex;
        double includedSeconds = ordered[pocIndex].Seconds;
        double target = totalSeconds * valueAreaFraction;
        while (includedSeconds < target && (lowIdx > 0 || highIdx < ordered.Count - 1))
        {
            double belowSeconds = lowIdx > 0 ? ordered[lowIdx - 1].Seconds : -1;
            double aboveSeconds = highIdx < ordered.Count - 1 ? ordered[highIdx + 1].Seconds : -1;
            if (aboveSeconds >= belowSeconds)
            {
                highIdx++;
                includedSeconds += ordered[highIdx].Seconds;
            }
            else
            {
                lowIdx--;
                includedSeconds += ordered[lowIdx].Seconds;
            }
        }

        return new TimeProfileSummary(
            ordered[pocIndex].Price + bucketWidth / 2.0,
            ordered[highIdx].Price + bucketWidth,
            ordered[lowIdx].Price);
    }
}
