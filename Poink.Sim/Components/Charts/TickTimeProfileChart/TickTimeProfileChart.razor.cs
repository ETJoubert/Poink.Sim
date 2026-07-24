using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Charts
{
    // One tick's timestamp paired with its mid price -- the minimum TickTimeProfileChart
    // needs from a raw Tick (Poink.Sim.Repository.Tick) to measure dwell time between
    // consecutive ticks. Its own public type rather than exposing Tick itself, same
    // reasoning as TickVolumeProfileChart's WeightedPriceSample: a public [Parameter]
    // can't expose a less-accessible type, and Tick is internal.
    public readonly record struct TimedPrice(DateTime Timestamp, double Price);

    // One price level's contribution to the profile, measured in seconds spent there
    // rather than tick count -- Count still rides along purely so the "enough data yet"
    // floor (MinSamplesPerBucket below) can gate on sample count like
    // TickVolumeProfileChart does, independent of how much wall-clock time those samples
    // happen to span.
    public readonly record struct WeightedTimeSample(double Price, int Count, double Seconds, double UpSeconds, double DownSeconds);

    // Horizontal-bar time histogram -- same "price on the row axis" layout and POC/Value
    // Area readout as TickVolumeProfileChart, but each bar's width is how long (in
    // seconds) price dwelled in that bucket rather than how many ticks landed there. Two
    // ticks a minute apart at the same price count for far more here than fifty ticks in
    // the same millisecond, which volume profile alone can't tell apart.
    // Raw-tick mode measures dwell time directly from consecutive tick timestamps
    // (PlainSamples). Bar mode has no per-tick timestamps to fall back on (only
    // per-bucket tick counts, see BarPriceHistogram), so WeightedSamples' Seconds figures
    // are approximated from each bar's own average tick interval instead (see
    // BarPriceHistogram.MergeTime).
    public partial class TickTimeProfileChart : ComponentBase
    {
        private const int MaxWindow = 5000; // matches SimulationEngine.MaxRecentTicks / SimulationPage.MaxTickWindow
        private const int MaxBuckets = 100;

        // Same floor TickVolumeProfileChart applies, gated on sample count rather than
        // total seconds so a fast-ticking window (lots of samples, little elapsed time)
        // doesn't get treated as "not enough data".
        private const int MinSamplesPerBucket = 3;

        [Parameter] public IReadOnlyList<TimedPrice> Ticks { get; set; } = Array.Empty<TimedPrice>();
        [Parameter] public double? CurrentPrice { get; set; }

        // Same "owned by the parent page" shared window control as TickVolumeProfileChart's
        // WindowSize -- only used for the plain Ticks path below (raw-tick mode); ignored
        // when WeightedSamples is supplied.
        [Parameter] public int WindowSize { get; set; } = 200;

        // Pre-aggregated alternative to Ticks/WindowSize -- SimulationPage supplies this in
        // bar mode, where a window can span far more ticks than raw timestamps could ever
        // be held for. See BarPriceHistogram.MergeTime for how it's derived.
        [Parameter] public IReadOnlyList<WeightedTimeSample>? WeightedSamples { get; set; }

        [Parameter] public bool ShowLevels { get; set; } = true;

        private bool _splitUpDown;
        private int _bucketCount = 20;

        private int ClampedWindowSize => Math.Clamp(WindowSize, 2, MaxWindow);

        private sealed record Bucket(
            double Low, double High, double Seconds, double Percent, double UpSeconds, double DownSeconds, double UpPercent, double DownPercent,
            bool IsCurrent, bool IsPoc, bool IsValueArea)
        {
            public double DiffSeconds => UpSeconds - DownSeconds;
        }

        private TimeProfileLevels? Levels => ShowLevels ? TimeProfileStats.Compute(WeightedSamples ?? PlainSamples()) : null;

        private static string DiffClass(double diff) => diff switch
        {
            > 0 => "ttp-row-count-diff-up",
            < 0 => "ttp-row-count-diff-down",
            _ => "",
        };

        private static string RowClass(Bucket b) =>
            $"{(b.IsCurrent ? "ttp-row-current" : "")} {(b.IsValueArea ? "ttp-row-value-area" : "")} {(b.IsPoc ? "ttp-row-poc" : "")}";

        // "12.4s" below a minute, "1m 04s" below an hour, "2h 15m" beyond that -- bar-mode
        // windows can span hours of wall-clock time even at a modest bar count.
        private static string FormatSeconds(double seconds)
        {
            if (seconds < 60) return seconds.ToString("0.#", CultureInfo.InvariantCulture) + "s";
            var span = TimeSpan.FromSeconds(seconds);
            return span.TotalHours >= 1
                ? $"{(int)span.TotalHours}h {span.Minutes:00}m"
                : $"{span.Minutes}m {span.Seconds:00}s";
        }

        private static string FormatSignedSeconds(double seconds) =>
            (seconds switch { > 0 => "+", < 0 => "-", _ => "" }) + FormatSeconds(Math.Abs(seconds));

        private List<Bucket> Buckets
        {
            get
            {
                var bucketCount = Math.Clamp(_bucketCount, 2, MaxBuckets);
                var samples = WeightedSamples ?? PlainSamples();

                var totalCount = samples.Sum(s => s.Count);
                if (totalCount < Math.Max(2, bucketCount * MinSamplesPerBucket)) return new List<Bucket>();

                var levels = Levels;

                double min = samples.Min(s => s.Price), max = samples.Max(s => s.Price);
                if (max <= min)
                {
                    var totalSeconds = samples.Sum(s => s.Seconds);
                    return new List<Bucket> { new(min, max, totalSeconds, 100, 0, 0, 0, 0, CurrentPrice is not null, levels is not null, levels is not null) };
                }

                var seconds = new double[bucketCount];
                var upSeconds = new double[bucketCount];
                var downSeconds = new double[bucketCount];
                double size = (max - min) / bucketCount;
                foreach (var s in samples)
                {
                    var idx = Math.Clamp((int)((s.Price - min) / size), 0, bucketCount - 1);
                    seconds[idx] += s.Seconds;
                    upSeconds[idx] += s.UpSeconds;
                    downSeconds[idx] += s.DownSeconds;
                }

                // Clamped rather than excluded when out of [min,max], same reasoning as
                // TickVolumeProfileChart -- the current price can briefly sit outside the
                // window's own range right as the window slides.
                var currentIdx = CurrentPrice is { } cp
                    ? Math.Clamp((int)((cp - min) / size), 0, bucketCount - 1)
                    : (int?)null;

                var maxSeconds = seconds.Max();
                var maxSplit = Math.Max(upSeconds.Max(), downSeconds.Max());
                var buckets = new List<Bucket>(bucketCount);
                for (var i = bucketCount - 1; i >= 0; i--)
                {
                    var low = min + size * i;
                    var high = low + size;
                    var pct = maxSeconds <= 0 ? 0 : seconds[i] * 100.0 / maxSeconds;
                    var upPct = maxSplit <= 0 ? 0 : upSeconds[i] * 100.0 / maxSplit;
                    var downPct = maxSplit <= 0 ? 0 : downSeconds[i] * 100.0 / maxSplit;
                    var isPoc = levels is { } l && low <= l.Poc && l.Poc < high;
                    var isValueArea = levels is { } va && low < va.ValueAreaHigh && high > va.ValueAreaLow;
                    buckets.Add(new Bucket(low, high, seconds[i], pct, upSeconds[i], downSeconds[i], upPct, downPct, i == currentIdx, isPoc, isValueArea));
                }
                return buckets;
            }
        }

        // Plain per-tick path: dwell time for tick i is the gap to tick i+1, attributed to
        // tick i's own price (the level the market was resting at during that gap), split
        // up/down by whether the next tick landed higher or lower. The trailing tick has no
        // known next tick within the window, so it contributes zero seconds -- it's still
        // included (Count=1, Seconds=0) purely so its price counts toward the profile's
        // min/max range.
        private List<WeightedTimeSample> PlainSamples()
        {
            var windowSize = ClampedWindowSize;
            var window = Ticks.Count > windowSize ? Ticks.Skip(Ticks.Count - windowSize).ToList() : Ticks;
            var samples = new List<WeightedTimeSample>(window.Count);
            for (var i = 0; i < window.Count; i++)
            {
                if (i == window.Count - 1)
                {
                    samples.Add(new WeightedTimeSample(window[i].Price, 1, 0, 0, 0));
                    continue;
                }
                var dwell = Math.Max(0, (window[i + 1].Timestamp - window[i].Timestamp).TotalSeconds);
                var up = window[i + 1].Price > window[i].Price;
                var down = window[i + 1].Price < window[i].Price;
                samples.Add(new WeightedTimeSample(window[i].Price, 1, dwell, up ? dwell : 0, down ? dwell : 0));
            }
            return samples;
        }
    }
}
