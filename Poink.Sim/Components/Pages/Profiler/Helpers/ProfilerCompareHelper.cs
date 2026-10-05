using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Charts;
using Poink.Sim.Helpers;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Profiler
{
	/// <summary>Builds the Profiler compare chart's lines from each symbol's loaded bars.</summary>
	internal static class ProfilerCompareHelper
	{
		public const string RatioColorVar = "--color-warning";

		public static string RatioLabel(string numerator, string denominator) => $"{numerator} ÷ {denominator}";

		public static List<CompareChartSeries> Build(
			IReadOnlyList<ProfilerSymbolData> sets,
			IReadOnlyDictionary<string, string> symbolColors,
			ProfilerCompareMode mode,
			DateTime? anchor,
			string? ratioTable,
			CompareChartSeriesStyle style
		)
		{
			var series = sets
				.Select(set => SymbolSeries(set, SymbolColorHelper.ColorVarFor(symbolColors, set.Table), mode, anchor, style))
				.ToList();

			if (sets.Count > 1 && sets.Skip(1).FirstOrDefault(s => s.Table == ratioTable) is { } denominator)
			{
				var numerator = sets[0];
				series.Add(
					new CompareChartSeries(RatioLabel(numerator.Table, denominator.Table), RatioColorVar, RatioLine(numerator, denominator), Pane: 1)
				);
			}

			return series;
		}

		/// <summary>Every loaded symbol's footprint over [start, end], in % rows from its own close at or before <paramref name="anchor"/>.</summary>
		public static CompareXGridResult? BuildXGrid(
			IReadOnlyList<ProfilerSymbolData> sets,
			IReadOnlyDictionary<string, string> symbolColors,
			DateTime start,
			DateTime end,
			DateTime anchor,
			double rowPercent,
			int colCount
		)
		{
			var inputs = sets
				.Where(s => s.HasBars)
				.Select(s => new CompareXGridInput(
					s.Table,
					SymbolColorHelper.ColorVarFor(symbolColors, s.Table),
					s.Bars,
					s.Histograms,
					ReferenceClose(s, anchor, start)
				))
				.ToList();
			return CompareXGridStats.Compute(inputs, start, end, rowPercent, colCount);
		}

		// A symbol with no bar by the anchor (its data starts later) is measured from its first bar inside the window instead.
		private static double ReferenceClose(ProfilerSymbolData set, DateTime anchor, DateTime windowStart) =>
			set.BarAtOrBefore(anchor)?.Close ?? set.Bars.FirstOrDefault(b => b.OpenTime >= windowStart).Close;

		private static CompareChartSeries SymbolSeries(
			ProfilerSymbolData set,
			string colorVar,
			ProfilerCompareMode mode,
			DateTime? anchor,
			CompareChartSeriesStyle style
		)
		{
			Func<double, double> scale = mode == ProfilerCompareMode.WindowAnchored ? PercentScale(set, anchor) : price => price;
			var points = set.Bars.Select(b => new ChartLinePoint(b.OpenTime, scale(b.Close))).ToList();
			var bars = style == CompareChartSeriesStyle.Bars
				? set.Bars.Select(b => new CTraderBarRow(b.OpenTime, scale(b.Open), scale(b.High), scale(b.Low), scale(b.Close), b.Volume)).ToList()
				: null;

			return new CompareChartSeries(set.Table, colorVar, points, Bars: bars);
		}

		// % change from the close of the bar at the anchor, or the first loaded bar when there is no anchor.
		private static Func<double, double> PercentScale(ProfilerSymbolData set, DateTime? anchor)
		{
			var baseClose = set.HasBars ? (anchor is { } start ? set.BarAtOrBefore(start) : null)?.Close ?? set.Bars[0].Close : 0;
			return baseClose == 0 ? _ => 0 : price => (price / baseClose - 1) * 100;
		}

		// Joins on exact OpenTime, which holds because every symbol's bars are floored to the same bar-size buckets.
		private static List<ChartLinePoint> RatioLine(ProfilerSymbolData numerator, ProfilerSymbolData denominator)
		{
			var denominatorCloses = denominator.Bars
				.Where(b => b.Close != 0)
				.DistinctBy(b => b.OpenTime)
				.ToDictionary(b => b.OpenTime, b => b.Close);

			return numerator.Bars
				.Where(b => denominatorCloses.ContainsKey(b.OpenTime))
				.Select(b => new ChartLinePoint(b.OpenTime, b.Close / denominatorCloses[b.OpenTime]))
				.ToList();
		}
	}
}
