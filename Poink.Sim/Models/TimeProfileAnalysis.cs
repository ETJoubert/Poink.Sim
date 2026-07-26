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
    double PointOfControlOffset, double ValueAreaHighOffset, double ValueAreaLowOffset);

internal static class TimeProfileAnalysis
{
    // Re-bins the merged buckets into a fixed-width histogram of seconds
    // fraction-of-total, ascending by price -- same shape/edge-case handling
    // as VolumeProfileAnalysis.BuildRelativeHistogram, weighted by Seconds
    // instead of Count. Empty/zero-time input -> all zeros (neutral).
    public static float[] BuildRelativeHistogram(
        IReadOnlyList<(double Price, int Count, double Seconds, double UpSeconds, double DownSeconds)> buckets, int bucketCount)
    {
        var histogram = new float[bucketCount];
        if (buckets.Count == 0) return histogram;

        double totalSeconds = buckets.Sum(b => b.Seconds);
        if (totalSeconds <= 0) return histogram;

        double min = buckets.Min(b => b.Price), max = buckets.Max(b => b.Price);
        if (max <= min)
        {
            // Every sample landed in one price bucket -- no range to spread
            // across, park it all in the middle bin (same convention as
            // VolumeProfileAnalysis).
            histogram[bucketCount / 2] = 1f;
            return histogram;
        }

        double size = (max - min) / bucketCount;
        var seconds = new double[bucketCount];
        foreach (var bucket in buckets)
        {
            int idx = Math.Clamp((int)((bucket.Price - min) / size), 0, bucketCount - 1);
            seconds[idx] += bucket.Seconds;
        }

        for (int i = 0; i < bucketCount; i++)
            histogram[i] = (float)(seconds[i] / totalSeconds);
        return histogram;
    }

    // Same value-area-expansion algorithm as VolumeProfileAnalysis.ComputeSummary,
    // ranked by seconds spent at a price level instead of tick count -- the
    // Point of Control is the price the market dwelled at longest, not
    // necessarily the one with the most ticks. Empty/zero-time input -> all
    // zeros, same neutral convention as BuildRelativeHistogram.
    public static TimeProfileSummary ComputeSummary(
        IReadOnlyList<(double Price, int Count, double Seconds, double UpSeconds, double DownSeconds)> buckets,
        double bucketWidth, double currentPrice, double valueAreaFraction = 0.70)
    {
        if (buckets.Count == 0 || currentPrice == 0) return default;

        var ordered = buckets.OrderBy(b => b.Price).ToList();
        double totalSeconds = ordered.Sum(b => b.Seconds);
        if (totalSeconds <= 0) return default;

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

        double pocPrice = ordered[pocIndex].Price + bucketWidth / 2.0;
        double valueAreaHigh = ordered[highIdx].Price + bucketWidth;
        double valueAreaLow = ordered[lowIdx].Price;

        return new TimeProfileSummary(
            (pocPrice - currentPrice) / currentPrice,
            (valueAreaHigh - currentPrice) / currentPrice,
            (valueAreaLow - currentPrice) / currentPrice);
    }
}
