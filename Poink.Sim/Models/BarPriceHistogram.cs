
using System;
using System.Collections.Generic;

namespace Poink.Sim.Models;

internal readonly record struct PriceBucketCounts(
    int Count, int UpCount, int DownCount, double UpDistance, double DownDistance,
    double UpPriceSum, double DownPriceSum);

internal sealed class BarPriceHistogram
{
    private readonly Dictionary<long, PriceBucketCounts> _buckets = new();

    public BarPriceHistogram(double bucketWidth) => BucketWidth = bucketWidth;

    public double BucketWidth { get; }

    public IReadOnlyDictionary<long, PriceBucketCounts> Buckets => _buckets;

    // The tick's count, move distance, and the origin price itself all book to the origin
    // bucket -- the cell the tick left, not the cell it arrived in. UpPriceSum/DownPriceSum
    // accumulate that origin price so SBuy/SSell (see XGridStats) sum the price before each
    // up/down move exactly, not at bucket resolution.
    public void Add(double originPrice, int direction, double distance)
    {
        long key = (long)Math.Floor(originPrice / BucketWidth);
        _buckets.TryGetValue(key, out var existing);
        _buckets[key] = new PriceBucketCounts(
            existing.Count + 1,
            existing.UpCount + (direction > 0 ? 1 : 0),
            existing.DownCount + (direction < 0 ? 1 : 0),
            existing.UpDistance + (direction > 0 ? distance : 0),
            existing.DownDistance + (direction < 0 ? distance : 0),
            existing.UpPriceSum + (direction > 0 ? originPrice : 0),
            existing.DownPriceSum + (direction < 0 ? originPrice : 0));
    }

    public static List<(double Price, int Count, int UpCount, int DownCount)> Merge(
        IReadOnlyList<BarPriceHistogram> histograms, int windowCount)
    {
        var start = Math.Max(0, histograms.Count - windowCount);
        var merged = new Dictionary<long, PriceBucketCounts>();
        double width = 0;
        for (var i = start; i < histograms.Count; i++)
        {
            var hist = histograms[i];
            width = hist.BucketWidth;
            foreach (var (key, c) in hist.Buckets)
            {
                merged.TryGetValue(key, out var existing);
                merged[key] = new PriceBucketCounts(
                    existing.Count + c.Count, existing.UpCount + c.UpCount, existing.DownCount + c.DownCount,
                    existing.UpDistance + c.UpDistance, existing.DownDistance + c.DownDistance,
                    existing.UpPriceSum + c.UpPriceSum, existing.DownPriceSum + c.DownPriceSum);
            }
        }

        var result = new List<(double, int, int, int)>(merged.Count);
        foreach (var (key, c) in merged)
            result.Add((key * width, c.Count, c.UpCount, c.DownCount));
        return result;
    }


    public static List<(double Price, int Count, double Seconds, double UpSeconds, double DownSeconds)> MergeTime(
        IReadOnlyList<HlocAvdtBar> bars, IReadOnlyList<BarPriceHistogram> histograms, int windowCount)
    {
        var start = Math.Max(0, histograms.Count - windowCount);
        var merged = new Dictionary<long, (int Count, double Seconds, double UpSeconds, double DownSeconds)>();
        double width = 0;
        for (var i = start; i < histograms.Count; i++)
        {
            var hist = histograms[i];
            width = hist.BucketWidth;
            var bar = bars[i];
            var avgTickSeconds = Math.Max(0, (bar.CloseTime - bar.OpenTime).TotalSeconds) / Math.Max(1, bar.Volume);
            foreach (var (key, c) in hist.Buckets)
            {
                merged.TryGetValue(key, out var existing);
                merged[key] = (
                    existing.Count + c.Count,
                    existing.Seconds + c.Count * avgTickSeconds,
                    existing.UpSeconds + c.UpCount * avgTickSeconds,
                    existing.DownSeconds + c.DownCount * avgTickSeconds);
            }
        }

        var result = new List<(double, int, double, double, double)>(merged.Count);
        foreach (var (key, v) in merged)
            result.Add((key * width, v.Count, v.Seconds, v.UpSeconds, v.DownSeconds));
        return result;
    }
}
