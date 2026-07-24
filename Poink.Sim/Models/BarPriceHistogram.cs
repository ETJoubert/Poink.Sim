// ── Sparse per-bar price histogram ───────────────────────────────────────
//  Every bar built while aggregating (see SimulationBarAggregator) keeps its own
//  tick-count-per-price-level breakdown instead of only the scalar Open/High/Low/
//  Close/Volume HlocAvdtBar carries. Prices are quantized to BucketWidth so every
//  bar in a run shares the exact same bucket keys -- that's what makes it valid to
//  sum several bars' histograms together bucket-for-bucket (see Merge below) rather
//  than needing the original per-tick prices again.
//
//  This is what lets the volume-profile panel cover an arbitrarily long bar window
//  (even a 10-month bar) without retaining raw ticks for it: storage here scales
//  with how many distinct price levels a bar's ticks actually visited, not with how
//  many ticks occurred -- a bar with millions of ticks confined to a narrow price
//  band still only needs a few thousand entries.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;

namespace Poink.Sim.Models;

internal readonly record struct PriceBucketCounts(int Count, int UpCount, int DownCount);

internal sealed class BarPriceHistogram
{
    private readonly Dictionary<long, PriceBucketCounts> _buckets = new();

    public BarPriceHistogram(double bucketWidth) => BucketWidth = bucketWidth;

    public double BucketWidth { get; }

    public IReadOnlyDictionary<long, PriceBucketCounts> Buckets => _buckets;

    // direction: >0 uptick, <0 downtick, 0 first tick of the bar / unchanged -- same
    // tick-rule classification SimulationBarAggregator already uses for the bar-wide
    // UpVolume/DownVolume totals, just recorded per price level here instead.
    public void Add(double price, int direction)
    {
        long key = (long)Math.Floor(price / BucketWidth);
        _buckets.TryGetValue(key, out var existing);
        _buckets[key] = new PriceBucketCounts(
            existing.Count + 1,
            existing.UpCount + (direction > 0 ? 1 : 0),
            existing.DownCount + (direction < 0 ? 1 : 0));
    }

    // Sums the last windowCount histograms' buckets together, keyed by the same
    // quantized price levels, then converts each key back to its bucket's low-edge
    // price -- the shape TickVolumeProfileChart.WeightedSamples consumes. Assumes
    // every histogram in the list shares the same BucketWidth (true today since
    // SimulationEngine builds them all from one PriceBucketWidthFor lookup per run).
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
                    existing.Count + c.Count, existing.UpCount + c.UpCount, existing.DownCount + c.DownCount);
            }
        }

        var result = new List<(double, int, int, int)>(merged.Count);
        foreach (var (key, c) in merged)
            result.Add((key * width, c.Count, c.UpCount, c.DownCount));
        return result;
    }

    // Time-profile counterpart to Merge above, for TickTimeProfileChart's bar-mode
    // WeightedSamples. Bar mode has no per-tick timestamps to measure dwell time from
    // directly (only this histogram's per-bucket tick counts), so each bar's seconds are
    // approximated as its own average tick interval -- (CloseTime - OpenTime) / Volume --
    // times how many of its ticks landed in a given bucket. Assumes bars and histograms
    // are index-aligned 1:1 (true today: SimulationEngine.AppendBar/PublishSnapshot always
    // append/attach them together).
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
