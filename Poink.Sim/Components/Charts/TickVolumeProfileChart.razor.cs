using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Charts
{
    // One price level's contribution to the profile -- Count=1 with a single up-or-down
    // flag for a raw tick (see PlainSamples), or a merged multi-tick total for a bar-mode
    // bucket (see BarPriceHistogram.Merge, fed in via WeightedSamples below).
    public readonly record struct WeightedPriceSample(double Price, int Count, int UpCount, int DownCount);

    // Horizontal-bar tick histogram (price on the row axis, tick count as bar width) --
    // deliberately plain Razor/CSS rather than the vendored lightweight-charts library
    // OhlcvChart uses, since it isn't pixel-synced to that chart's zoom/pan; it just
    // shares the same "high price at top" orientation so it reads naturally beside it.
    // Recomputes from its Prices/WeightedSamples on every render, which is cheap at the
    // buffer's tick cap (raw-tick mode) or bar cap (bar mode).
    // Takes plain mid-prices rather than the internal Tick type, since a public
    // [Parameter] can't expose a less-accessible type.
    public partial class TickVolumeProfileChart : ComponentBase
    {
        private const int MaxWindow = 5000; // matches SimulationEngine.MaxRecentTicks / SimulationPage.MaxTickWindow
        private const int MaxBuckets = 100;

        // Below this many samples per bucket, maxCount-relative scaling degenerates --
        // e.g. right after Load/Reset, with 5 ticks spread across 20 buckets every
        // populated bucket has count 1, so count/maxCount renders every one of them at
        // 100% ("all bars maxed"). Below the floor we show a placeholder instead of that.
        private const int MinSamplesPerBucket = 3;

        [Parameter] public IReadOnlyList<double> Prices { get; set; } = Array.Empty<double>();
        [Parameter] public double? CurrentPrice { get; set; }

        // Owned by the parent page now (SimulationPage), not this component -- it also
        // drives the "window start" marker on the OhlcvChart candlestick chart, so a
        // single slider up there needs to control both rather than each chart having
        // its own out-of-sync copy. Only used for the plain Prices path below (raw-tick
        // mode); ignored when WeightedSamples is supplied.
        [Parameter] public int WindowSize { get; set; } = 200;

        // Pre-aggregated alternative to Prices/WindowSize -- SimulationPage supplies this
        // in bar mode, where a window can span far more ticks than could ever be held
        // individually (see BarPriceHistogram's doc comment). When set, this replaces
        // Prices/WindowSize entirely; when null, the plain per-tick path is used instead.
        [Parameter] public IReadOnlyList<WeightedPriceSample>? WeightedSamples { get; set; }

        private bool _splitUpDown;
        private int _bucketCount = 20;

        private int ClampedWindowSize => Math.Clamp(WindowSize, 2, MaxWindow);

        // Up/down counts classify each tick by direction vs. the prior tick (a standard
        // uptick/downtick volume proxy -- ticks carry no explicit trade-side field) and
        // are always computed alongside the plain total, so toggling split mode is a pure
        // presentation switch with no recompute.
        private sealed record Bucket(
            double Low, double High, int Count, double Percent, int UpCount, int DownCount, double UpPercent, double DownPercent, bool IsCurrent);

        private List<Bucket> Buckets
        {
            get
            {
                var bucketCount = Math.Clamp(_bucketCount, 2, MaxBuckets);
                var samples = WeightedSamples ?? PlainSamples();

                var totalCount = samples.Sum(s => s.Count);
                if (totalCount < Math.Max(2, bucketCount * MinSamplesPerBucket)) return new List<Bucket>();

                double min = samples.Min(s => s.Price), max = samples.Max(s => s.Price);
                if (max <= min) return new List<Bucket> { new(min, max, totalCount, 100, 0, 0, 0, 0, CurrentPrice is not null) };

                var counts = new int[bucketCount];
                var upCounts = new int[bucketCount];
                var downCounts = new int[bucketCount];
                double size = (max - min) / bucketCount;
                foreach (var s in samples)
                {
                    var idx = Math.Clamp((int)((s.Price - min) / size), 0, bucketCount - 1);
                    counts[idx] += s.Count;
                    upCounts[idx] += s.UpCount;
                    downCounts[idx] += s.DownCount;
                }

                // Clamped rather than excluded when out of [min,max] -- the current price can
                // briefly sit outside the window's own range right as the window slides, and
                // pinning it to the nearest edge bucket beats not highlighting anything at all.
                var currentIdx = CurrentPrice is { } cp
                    ? Math.Clamp((int)((cp - min) / size), 0, bucketCount - 1)
                    : (int?)null;

                var maxCount = counts.Max();
                var maxSplit = Math.Max(upCounts.Max(), downCounts.Max());
                var buckets = new List<Bucket>(bucketCount);
                for (var i = bucketCount - 1; i >= 0; i--)
                {
                    var low = min + size * i;
                    var high = low + size;
                    var pct = maxCount == 0 ? 0 : counts[i] * 100.0 / maxCount;
                    var upPct = maxSplit == 0 ? 0 : upCounts[i] * 100.0 / maxSplit;
                    var downPct = maxSplit == 0 ? 0 : downCounts[i] * 100.0 / maxSplit;
                    buckets.Add(new Bucket(low, high, counts[i], pct, upCounts[i], downCounts[i], upPct, downPct, i == currentIdx));
                }
                return buckets;
            }
        }

        // Plain per-tick path: every raw price is its own sample (Count=1), with up/down
        // classified against the immediately preceding tick in the window -- the same
        // tick-rule bar mode's WeightedSamples already bakes in per-tick before ticks are
        // discarded (see SimulationBarAggregator/BarPriceHistogram).
        private List<WeightedPriceSample> PlainSamples()
        {
            var windowSize = ClampedWindowSize;
            var prices = Prices.Count > windowSize ? Prices.Skip(Prices.Count - windowSize).ToList() : Prices;
            var samples = new List<WeightedPriceSample>(prices.Count);
            for (var i = 0; i < prices.Count; i++)
            {
                var up = i > 0 && prices[i] > prices[i - 1] ? 1 : 0;
                var down = i > 0 && prices[i] < prices[i - 1] ? 1 : 0;
                samples.Add(new WeightedPriceSample(prices[i], 1, up, down));
            }
            return samples;
        }
    }
}
