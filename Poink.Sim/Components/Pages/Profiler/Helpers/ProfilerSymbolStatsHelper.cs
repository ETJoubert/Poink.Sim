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

		public static List<ChartHorizontalLine> ProfileLevelLines(IReadOnlyList<WeightedPriceSample> samples) =>
			VolumeProfileStats.Compute(samples) is { } levels
				? new()
				{
					new ChartHorizontalLine(levels.Poc, "POC", "profile"),
					new ChartHorizontalLine(levels.ValueAreaHigh, "VAH", "profile"),
					new ChartHorizontalLine(levels.ValueAreaLow, "VAL", "profile"),
				}
				: new();

		public static List<ChartHorizontalLine> AverageMedianLines(IReadOnlyList<WeightedPriceSample> samples, bool showAverage, bool showMedian)
		{
			var lines = new List<ChartHorizontalLine>();
			if (showAverage && VolumeProfileStats.ComputeAverage(samples) is { } average)
				lines.Add(new ChartHorizontalLine(average, "AVG", "average"));
			if (showMedian && VolumeProfileStats.ComputeMedian(samples) is { } median)
				lines.Add(new ChartHorizontalLine(median, "MED", "median"));
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
