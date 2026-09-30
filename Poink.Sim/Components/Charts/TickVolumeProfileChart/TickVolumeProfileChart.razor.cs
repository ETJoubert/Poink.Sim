using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Helpers;

namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// One price level's contribution to the profile: Count=1 with a single up-or-down flag for a raw
	/// tick, or a merged multi-tick total for a bar-mode bucket (see BarPriceHistogram.Merge).
	/// </summary>
	public readonly record struct WeightedPriceSample(double Price, int Count, int UpCount, int DownCount);

	/// <summary>
	/// Horizontal-bar tick histogram (price on the row axis, tick count as bar width), deliberately plain
	/// Razor/CSS rather than the vendored lightweight-charts library, since it just shares OhlcvChart's
	/// "high price at top" orientation without being pixel-synced to its zoom/pan. Takes plain mid-prices
	/// rather than the internal Tick type, since a public [Parameter] can't expose a less-accessible type.
	/// </summary>
	public partial class TickVolumeProfileChart : ComponentBase
	{
		// Matches SimulationEngine.MaxRecentTicks / SimulationPage.MaxTickWindow.
		private const int MaxWindow = 5000;
		private const int MaxBuckets = 100;

		// Below this floor, count/maxCount scaling degenerates -- sparse buckets all read as 100%.
		private const int MinSamplesPerBucket = 3;

		[Parameter]
		public IReadOnlyList<double> Prices { get; set; } = Array.Empty<double>();

		[Parameter]
		public double? CurrentPrice { get; set; }

		/// <summary>
		/// Owned by the parent page (SimulationPage), which also drives the OhlcvChart "window start"
		/// marker from the same slider. Only used for the plain Prices path; ignored when WeightedSamples is supplied.
		/// </summary>
		[Parameter]
		public int WindowSize { get; set; } = 200;

		/// <summary>
		/// Pre-aggregated alternative to Prices/WindowSize; SimulationPage supplies this in bar mode, where
		/// a window can span far more ticks than could ever be held individually. When set, this replaces
		/// Prices/WindowSize entirely.
		/// </summary>
		[Parameter]
		public IReadOnlyList<WeightedPriceSample>? WeightedSamples { get; set; }

		/// <summary>
		/// Caller-controlled visibility for the Point of Control/Value Area highlight and header readout;
		/// doesn't affect WeightedSamples/Buckets, only whether Levels ever computes.
		/// </summary>
		[Parameter]
		public bool ShowLevels { get; set; } = true;

		private bool _splitUpDown;
		private int _bucketCount = 20;

		private int ClampedWindowSize => Math.Clamp(WindowSize, 2, MaxWindow);

		// Up/down counts classify each tick by direction vs. the prior tick; ticks carry no explicit trade-side field.
		private sealed record Bucket(
			double Low,
			double High,
			int Count,
			double Percent,
			int UpCount,
			int DownCount,
			double UpPercent,
			double DownPercent,
			bool IsCurrent,
			bool IsPoc,
			bool IsValueArea
		)
		{
			public int Diff => UpCount - DownCount;
		}

		// Null while there isn't enough data for a meaningful Point of Control/Value Area, or while ShowLevels is off.
		private VolumeProfileLevels? Levels =>
			ShowLevels ? VolumeProfileStats.Compute(WeightedSamples ?? PlainSamples()) : null;

		// Matches the up/down bars' own colors, same convention as SimulationPage's PnlClass.
		private static string DiffClass(int diff) =>
			diff switch
			{
				> 0 => "tvp-row-count-diff-up",
				< 0 => "tvp-row-count-diff-down",
				_ => "",
			};

		// Combinable, not exclusive -- the current-price row can also be the POC row or sit inside the value area.
		private static string RowClass(Bucket b) =>
			$"{(b.IsCurrent ? "tvp-row-current" : "")} {(b.IsValueArea ? "tvp-row-value-area" : "")} {(b.IsPoc ? "tvp-row-poc" : "")}";

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
					return new List<Bucket>
					{
						new(
							min,
							max,
							totalCount,
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

				double size = (max - min) / bucketCount;
				var counts = RangeStatsHelper.BucketWeights(samples, s => s.Price, s => (double)s.Count, min, size, bucketCount);
				var upCounts = RangeStatsHelper.BucketWeights(samples, s => s.Price, s => (double)s.UpCount, min, size, bucketCount);
				var downCounts = RangeStatsHelper.BucketWeights(samples, s => s.Price, s => (double)s.DownCount, min, size, bucketCount);

				// Clamped rather than excluded -- pinning it to the nearest edge bucket beats not highlighting anything.
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
					var isPoc = levels is { } l && low <= l.Poc && l.Poc < high;
					var isValueArea = levels is { } va && low < va.ValueAreaHigh && high > va.ValueAreaLow;
					buckets.Add(
						new Bucket(
							low,
							high,
							(int)counts[i],
							pct,
							(int)upCounts[i],
							(int)downCounts[i],
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

		// Every raw price is its own sample (Count=1), up/down classified against the immediately preceding tick.
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
