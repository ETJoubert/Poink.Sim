using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Charts;
using Poink.Sim.Helpers;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Profiler
{
	/// <summary>
	/// Profile samples and chart overlays for one <see cref="ProfilerSymbolData"/>, narrowed to the Lookback Window
	/// when one is set and spanning every loaded bar otherwise.
	/// </summary>
	internal static class ProfilerSymbolStatsHelper
	{
		public static List<WeightedPriceSample> MergedSamples(ProfilerSymbolData data, DateTime? start, DateTime? end)
		{
			var histograms = WindowedPairs(data, start, end).Select(t => t.Histogram).ToList();
			return BarPriceHistogram
				.Merge(histograms, histograms.Count)
				.Select(t => new WeightedPriceSample(t.Price, t.Count, t.UpCount, t.DownCount))
				.ToList();
		}

		public static List<WeightedTimeSample> MergedTimeSamples(ProfilerSymbolData data, DateTime? start, DateTime? end)
		{
			var pairs = WindowedPairs(data, start, end).ToList();
			var histograms = pairs.Select(t => t.Histogram).ToList();
			return BarPriceHistogram
				.MergeTime(pairs.Select(t => t.Bar).ToList(), histograms, histograms.Count)
				.Select(t => new WeightedTimeSample(t.Price, t.Count, t.Seconds, t.UpSeconds, t.DownSeconds))
				.ToList();
		}

		public static List<ChartLinePoint> Vwap(ProfilerSymbolData data, DateTime? start, DateTime? end) =>
			VectorStatsHelper
				.Vwap(WindowedPairs(data, start, end).Select(t => t.Bar), b => b.OpenTime, b => b.Average, b => b.Volume)
				.Select(p => new ChartLinePoint(p.TimeUtc, p.Value))
				.ToList();

		public static List<ChartHorizontalLine> ProfileLevelLines(IReadOnlyList<WeightedPriceSample> samples, bool showPoc, bool showVah, bool showVal)
		{
			var lines = new List<ChartHorizontalLine>();
			if ((!showPoc && !showVah && !showVal) || VolumeProfileStats.Compute(samples) is not { } levels)
				return lines;

			if (showPoc)
				lines.Add(new ChartHorizontalLine(levels.Poc, "POC", "profile"));
			if (showVah)
				lines.Add(new ChartHorizontalLine(levels.ValueAreaHigh, "VAH", "profile"));
			if (showVal)
				lines.Add(new ChartHorizontalLine(levels.ValueAreaLow, "VAL", "profile"));
			return lines;
		}

		public static List<ChartHorizontalLine> AverageMedianLines(IReadOnlyList<WeightedPriceSample> samples, bool showAverage, bool showMedian)
		{
			var lines = new List<ChartHorizontalLine>();
			if (showAverage && VolumeProfileStats.ComputeAverage(samples) is { } average)
				lines.Add(new ChartHorizontalLine(average, "AVG", "average"));
			if (showMedian && VolumeProfileStats.ComputeMedian(samples) is { } median)
				lines.Add(new ChartHorizontalLine(median, "MED", "median"));
			return lines;
		}

		/// <summary>The least-squares fit of close prices across the window, drawn from its first bar to its last.</summary>
		public static ChartDiagonalLine? RegressionLine(ProfilerSymbolData data, DateTime? start, DateTime? end)
		{
			var bars = WindowedPairs(data, start, end).Select(t => t.Bar).ToList();
			if (bars.Count < 2)
				return null;

			var (slope, intercept) = RangeStatsHelper.LinearFit(bars.Select(b => b.Close).ToList());
			return new ChartDiagonalLine(bars[0].OpenTime, intercept, bars[^1].OpenTime, slope * (bars.Count - 1) + intercept, "REG", "regression");
		}

		/// <summary>A vertical line at every bar that opens in a new <paramref name="hours"/>-hour bucket aligned to midnight, matching XGrid's day/interval lines.</summary>
		public static List<ChartVerticalLine> TimeBoundaryLines(ProfilerSymbolData data, int hours, string colorGroup)
		{
			var lines = new List<ChartVerticalLine>();
			if (hours <= 0)
				return lines;

			long BucketOf(DateTime t) => t.Ticks / (TimeSpan.TicksPerHour * hours);
			for (var i = 1; i < data.Bars.Count; i++)
				if (BucketOf(data.Bars[i].OpenTime) != BucketOf(data.Bars[i - 1].OpenTime))
					lines.Add(new ChartVerticalLine(data.Bars[i].OpenTime, null, colorGroup));
			return lines;
		}

		private static IEnumerable<(HlocAvdtBar Bar, BarPriceHistogram Histogram)> WindowedPairs(
			ProfilerSymbolData data,
			DateTime? start,
			DateTime? end
		)
		{
			var pairs = data.Bars.Zip(data.Histograms, (bar, histogram) => (bar, histogram));
			return start is { } s && end is { } e ? pairs.Where(t => t.bar.OpenTime >= s && t.bar.OpenTime <= e) : pairs;
		}
	}
}
