using System;
using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Components.Charts;

// Point of Control (the price with the most volume) and Value Area High/Low (the
// tightest band holding ValueAreaFraction of total volume) -- the standard "where did
// this profile actually trade" read, on top of TickVolumeProfileChart's own per-bucket
// bars. Fixed BucketCount rather than reusing the panel's own user-adjustable bucket
// count, so this stays a stable summary instead of shifting with a purely cosmetic
// display setting.
internal readonly record struct VolumeProfileLevels(double Poc, double ValueAreaHigh, double ValueAreaLow);

internal static class VolumeProfileStats
{
    private const int BucketCount = 50;
    private const double ValueAreaFraction = 0.70;

    // Same "too little data to mean anything" floor TickVolumeProfileChart.Buckets
    // applies to its own (variable) bucket count -- scaled to this type's fixed one.
    private const int MinSamplesPerBucket = 3;

    public static VolumeProfileLevels? Compute(IReadOnlyList<WeightedPriceSample> samples)
    {
        var totalCount = samples.Sum(s => s.Count);
        if (totalCount < BucketCount * MinSamplesPerBucket) return null;

        double min = samples.Min(s => s.Price), max = samples.Max(s => s.Price);
        if (max <= min) return new VolumeProfileLevels(min, min, min);

        var counts = new int[BucketCount];
        var size = (max - min) / BucketCount;
        foreach (var s in samples)
        {
            var idx = Math.Clamp((int)((s.Price - min) / size), 0, BucketCount - 1);
            counts[idx] += s.Count;
        }

        var pocIndex = 0;
        for (var i = 1; i < BucketCount; i++)
            if (counts[i] > counts[pocIndex]) pocIndex = i;

        // Expands one bucket at a time from the POC, each step taking whichever
        // neighbour (above the current band or below it) carries more volume, until
        // the band covers ValueAreaFraction of total volume or both edges are hit.
        var lowIndex = pocIndex;
        var highIndex = pocIndex;
        var covered = counts[pocIndex];
        var target = totalCount * ValueAreaFraction;
        while (covered < target && (lowIndex > 0 || highIndex < BucketCount - 1))
        {
            var belowCount = lowIndex > 0 ? counts[lowIndex - 1] : -1;
            var aboveCount = highIndex < BucketCount - 1 ? counts[highIndex + 1] : -1;

            if (aboveCount >= belowCount)
            {
                highIndex++;
                covered += counts[highIndex];
            }
            else
            {
                lowIndex--;
                covered += counts[lowIndex];
            }
        }

        return new VolumeProfileLevels(min + size * (pocIndex + 0.5), min + size * (highIndex + 1), min + size * lowIndex);
    }

    // Exact (unbucketed) mean price, weighted by each sample's tick count.
    public static double? ComputeAverage(IReadOnlyList<WeightedPriceSample> samples)
    {
        var totalCount = samples.Sum(s => s.Count);
        return totalCount == 0 ? null : samples.Sum(s => s.Price * (double)s.Count) / totalCount;
    }

    // Exact (unbucketed) median price -- the sample where cumulative count first
    // reaches half of the total, walking samples in ascending price order.
    public static double? ComputeMedian(IReadOnlyList<WeightedPriceSample> samples)
    {
        var totalCount = samples.Sum(s => s.Count);
        if (totalCount == 0) return null;

        var half = totalCount / 2.0;
        long cumulative = 0;
        foreach (var s in samples.OrderBy(s => s.Price))
        {
            cumulative += s.Count;
            if (cumulative >= half) return s.Price;
        }
        return samples.Max(s => s.Price);
    }
}
