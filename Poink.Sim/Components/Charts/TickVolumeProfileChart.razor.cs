using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Charts
{
    // Horizontal-bar tick histogram (price on the row axis, tick count as bar width) --
    // deliberately plain Razor/CSS rather than the vendored lightweight-charts library
    // OhlcvChart uses, since it isn't pixel-synced to that chart's zoom/pan; it just
    // shares the same "high price at top" orientation so it reads naturally beside it.
    // Recomputes from Prices on every render, which is cheap at the buffer's 500-tick cap.
    // Takes plain mid-prices rather than the internal Tick type, since a public
    // [Parameter] can't expose a less-accessible type.
    public partial class TickVolumeProfileChart : ComponentBase
    {
        private const int MaxWindow = 500; // matches SimulationEngine.MaxRecentTicks
        private const int MaxBuckets = 100;

        // Below this many samples per bucket, maxCount-relative scaling degenerates --
        // e.g. right after Load/Reset, with 5 ticks spread across 20 buckets every
        // populated bucket has count 1, so count/maxCount renders every one of them at
        // 100% ("all bars maxed"). Below the floor we show a placeholder instead of that.
        private const int MinSamplesPerBucket = 3;

        [Parameter] public IReadOnlyList<double> Prices { get; set; } = Array.Empty<double>();

        private bool _showSettings;
        private int _bucketCount = 20;
        private int _windowSize = 200;

        private void ToggleSettings() => _showSettings = !_showSettings;

        private sealed record Bucket(double Low, double High, int Count, double Percent);

        private List<Bucket> Buckets
        {
            get
            {
                var bucketCount = Math.Clamp(_bucketCount, 2, MaxBuckets);
                var windowSize = Math.Clamp(_windowSize, 2, MaxWindow);

                var prices = Prices.Count > windowSize ? Prices.Skip(Prices.Count - windowSize).ToList() : Prices;
                if (prices.Count < Math.Max(2, bucketCount * MinSamplesPerBucket)) return new List<Bucket>();

                double min = prices.Min(), max = prices.Max();
                if (max <= min) return new List<Bucket> { new(min, max, prices.Count, 100) };

                var counts = new int[bucketCount];
                double size = (max - min) / bucketCount;
                foreach (var p in prices)
                {
                    var idx = Math.Clamp((int)((p - min) / size), 0, bucketCount - 1);
                    counts[idx]++;
                }

                var maxCount = counts.Max();
                var buckets = new List<Bucket>(bucketCount);
                for (var i = bucketCount - 1; i >= 0; i--)
                {
                    var low = min + size * i;
                    var high = low + size;
                    var pct = maxCount == 0 ? 0 : counts[i] * 100.0 / maxCount;
                    buckets.Add(new Bucket(low, high, counts[i], pct));
                }
                return buckets;
            }
        }
    }
}
