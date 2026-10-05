using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Profiler
{
	/// <summary>
	/// Profile samples and windowed bars for one <see cref="ProfilerSymbolData"/>, narrowed to the Lookback Window
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

		public static List<HlocAvdtBar> WindowedBars(ProfilerSymbolData data, DateTime? start, DateTime? end) =>
			WindowedPairs(data, start, end).Select(t => t.Bar).ToList();

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
