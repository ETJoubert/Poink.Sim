using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Helpers;

namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// One tick's timestamp paired with its mid price, the minimum needed to measure dwell time between
	/// consecutive ticks. A public type of its own -- like TickVolumeProfileChart's WeightedPriceSample --
	/// because a public [Parameter] can't expose the internal Tick type directly.
	/// </summary>
	public readonly record struct TimedPrice(DateTime Timestamp, double Price);

	/// <summary>
	/// One price level's contribution to the profile, measured in seconds spent there rather than tick
	/// count; Count still rides along so MinSamplesPerBucket can gate on sample count, independent of
	/// how much wall-clock time those samples happen to span.
	/// </summary>
	public readonly record struct WeightedTimeSample(
		double Price,
		int Count,
		double Seconds,
		double UpSeconds,
		double DownSeconds
	);

	/// <summary>
	/// Horizontal-bar time histogram, same "price on the row axis" layout and POC/Value Area readout as
	/// TickVolumeProfileChart, but each bar's width is how long price dwelled in that bucket rather than
	/// how many ticks landed there. Raw-tick mode measures dwell time directly from consecutive tick
	/// timestamps; bar mode approximates it from each bar's own average tick interval (see BarPriceHistogram.MergeTime).
	/// </summary>
	public partial class TickTimeProfileChart : ComponentBase
	{
		// Matches SimulationEngine.MaxRecentTicks / SimulationPage.MaxTickWindow.
		private const int MaxWindow = 5000;
		private const int MaxBuckets = 100;

		// Gated on sample count rather than total seconds so a fast-ticking window (lots of
		// samples, little elapsed time) doesn't get treated as "not enough data".
		private const int MinSamplesPerBucket = 3;

		[Parameter]
		public IReadOnlyList<TimedPrice> Ticks { get; set; } = Array.Empty<TimedPrice>();

		[Parameter]
		public double? CurrentPrice { get; set; }

		/// <summary>
		/// Shared window control, same as TickVolumeProfileChart's WindowSize -- only used for the plain
		/// Ticks path below (raw-tick mode); ignored when WeightedSamples is supplied.
		/// </summary>
		[Parameter]
		public int WindowSize { get; set; } = 200;

		/// <summary>
		/// Pre-aggregated alternative to Ticks/WindowSize; SimulationPage supplies this in bar mode, where
		/// a window can span far more ticks than raw timestamps could ever be held for.
		/// </summary>
		[Parameter]
		public IReadOnlyList<WeightedTimeSample>? WeightedSamples { get; set; }

		[Parameter]
		public bool ShowLevels { get; set; } = true;

		private bool _splitUpDown;
		private int _bucketCount = 20;

		private int ClampedWindowSize => Math.Clamp(WindowSize, 2, MaxWindow);

		private sealed record Bucket(
			double Low,
			double High,
			double Seconds,
			double Percent,
			double UpSeconds,
			double DownSeconds,
			double UpPercent,
			double DownPercent,
			bool IsCurrent,
			bool IsPoc,
			bool IsValueArea
		)
		{
			public double DiffSeconds => UpSeconds - DownSeconds;
		}

		private TimeProfileLevels? Levels =>
			ShowLevels ? TimeProfileHelper.Compute(WeightedSamples ?? PlainSamples(), s => s.Price, s => s.Count, s => s.Seconds) : null;

		private static string DiffClass(double diff) =>
			diff switch
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
			if (seconds < 60)
				return seconds.ToString("0.#", CultureInfo.InvariantCulture) + "s";
			var span = TimeSpan.FromSeconds(seconds);
			return span.TotalHours >= 1
				? $"{(int)span.TotalHours}h {span.Minutes:00}m"
				: $"{span.Minutes}m {span.Seconds:00}s";
		}

		private static string FormatSignedSeconds(double seconds) =>
			(
				seconds switch
				{
					> 0 => "+",
					< 0 => "-",
					_ => "",
				}
			) + FormatSeconds(Math.Abs(seconds));

		private List<Bucket> Buckets
		{
			get
			{
				var bucketCount = Math.Clamp(_bucketCount, 2, MaxBuckets);
				var samples = WeightedSamples ?? PlainSamples();

				var totalCount = samples.Sum(s => s.Count);
				if (totalCount < Math.Max(2, bucketCount * MinSamplesPerBucket))
					return new List<Bucket>();

				var levels = Levels;

				double min = samples.Min(s => s.Price),
					max = samples.Max(s => s.Price);
				if (max <= min)
				{
					var totalSeconds = samples.Sum(s => s.Seconds);
					return new List<Bucket>
					{
						new(
							min,
							max,
							totalSeconds,
							100,
							0,
							0,
							0,
							0,
							CurrentPrice is not null,
							levels is not null,
							levels is not null
						),
					};
				}

				double size = (max - min) / bucketCount;
				var seconds = RangeStatsHelper.BucketWeights(samples, s => s.Price, s => s.Seconds, min, size, bucketCount);
				var upSeconds = RangeStatsHelper.BucketWeights(samples, s => s.Price, s => s.UpSeconds, min, size, bucketCount);
				var downSeconds = RangeStatsHelper.BucketWeights(samples, s => s.Price, s => s.DownSeconds, min, size, bucketCount);

				// Clamped rather than excluded when out of range -- the current price can
				// briefly sit outside the window's own range right as the window slides.
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
					buckets.Add(
						new Bucket(
							low,
							high,
							seconds[i],
							pct,
							upSeconds[i],
							downSeconds[i],
							upPct,
							downPct,
							i == currentIdx,
							isPoc,
							isValueArea
						)
					);
				}
				return buckets;
			}
		}

		// Dwell time for tick i is the gap to tick i+1, attributed to tick i's own price.
		// The trailing tick contributes zero seconds but stays in for its price's min/max range.
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
