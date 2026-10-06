using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Charts
{
	/// <summary>Builds the compare chart's series from each symbol's bars.</summary>
	internal static class CompareSetHelper
	{
		public const string RatioColorVar = "--color-warning";

		public static string RatioLabel(string numerator, string denominator) => $"{numerator} ÷ {denominator}";

		public static List<CompareChartSeries> Build(
			IReadOnlyList<CompareSetSymbol> symbols,
			CompareSetMode mode,
			DateTime? anchor,
			string? ratioTable,
			CompareChartSeriesStyle style
		)
		{
			var series = symbols.Select(symbol => SymbolSeries(symbol, mode, anchor, style)).ToList();

			if (symbols.Count > 1 && symbols.Skip(1).FirstOrDefault(s => s.Table == ratioTable) is { } denominator)
			{
				var numerator = symbols[0];
				series.Add(
					new CompareChartSeries(RatioLabel(numerator.Table, denominator.Table), RatioColorVar, RatioLine(numerator, denominator), Pane: 1)
				);
			}

			return series;
		}

		public static CTraderBarRow? BarAtOrBefore(IReadOnlyList<CTraderBarRow> bars, DateTime time)
		{
			CTraderBarRow? found = null;
			foreach (var bar in bars)
			{
				if (bar.TimeUtc > time)
					break;
				found = bar;
			}
			return found;
		}

		private static CompareChartSeries SymbolSeries(
			CompareSetSymbol symbol,
			CompareSetMode mode,
			DateTime? anchor,
			CompareChartSeriesStyle style
		)
		{
			Func<double, double> scale = mode == CompareSetMode.WindowAnchored ? PercentScale(symbol.Bars, anchor) : price => price;
			var points = symbol.Bars.Select(b => new ChartLinePoint(b.TimeUtc, scale(b.Close))).ToList();
			var bars = style == CompareChartSeriesStyle.Bars
				? symbol.Bars.Select(b => new CTraderBarRow(b.TimeUtc, scale(b.Open), scale(b.High), scale(b.Low), scale(b.Close), b.Volume)).ToList()
				: null;

			return new CompareChartSeries(symbol.Table, symbol.ColorVar, points, Bars: bars);
		}

		// % change from the close of the bar at the anchor, or the first bar when there is none by then.
		private static Func<double, double> PercentScale(IReadOnlyList<CTraderBarRow> bars, DateTime? anchor)
		{
			var baseClose = bars.Count == 0 ? 0 : (anchor is { } time ? BarAtOrBefore(bars, time) : null)?.Close ?? bars[0].Close;
			return baseClose == 0 ? _ => 0 : price => (price / baseClose - 1) * 100;
		}

		// Joins on exact bar time, which holds because every symbol's bars are floored to the same bar-size buckets.
		private static List<ChartLinePoint> RatioLine(CompareSetSymbol numerator, CompareSetSymbol denominator)
		{
			var denominatorCloses = denominator.Bars
				.Where(b => b.Close != 0)
				.DistinctBy(b => b.TimeUtc)
				.ToDictionary(b => b.TimeUtc, b => b.Close);

			return numerator.Bars
				.Where(b => denominatorCloses.ContainsKey(b.TimeUtc))
				.Select(b => new ChartLinePoint(b.TimeUtc, b.Close / denominatorCloses[b.TimeUtc]))
				.ToList();
		}
	}
}
