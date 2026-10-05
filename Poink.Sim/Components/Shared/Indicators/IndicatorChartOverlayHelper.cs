using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Charts;
using Poink.Sim.Helpers;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Shared.Indicators
{
	/// <summary>The market-chart lines the visible indicators add, ready to merge with a host's own drawings.</summary>
	internal sealed record IndicatorChartOverlays(
		List<ChartHorizontalLine> HorizontalLines,
		List<ChartVerticalLine> VerticalLines,
		List<ChartDiagonalLine> DiagonalLines,
		List<ChartLinePoint> VwapLine
	)
	{
		public static IndicatorChartOverlays Empty => new(new(), new(), new(), new());
	}

	/// <summary>
	/// Builds the market chart's indicator lines so every host draws them the same way: price levels from the window's
	/// merged tick samples, VWAP and the regression fit from the window's bars, and day/interval lines across every loaded bar.
	/// </summary>
	internal static class IndicatorChartOverlayHelper
	{
		public static IndicatorChartOverlays Build(
			IndicatorSettings indicators,
			IReadOnlyList<HlocAvdtBar> loadedBars,
			IReadOnlyList<HlocAvdtBar> windowBars,
			IReadOnlyList<WeightedPriceSample> windowSamples
		)
		{
			if (!indicators.Enabled)
				return IndicatorChartOverlays.Empty;

			var horizontal = ProfileLevelLines(windowSamples, indicators.PocVisible, indicators.VahVisible, indicators.ValVisible)
				.Concat(AverageMedianLines(windowSamples, indicators.AverageVisible, indicators.MedianVisible))
				.ToList();

			var vertical = new List<ChartVerticalLine>();
			if (indicators.DayLinesVisible)
				vertical.AddRange(TimeBoundaryLines(loadedBars, 24, "day"));
			if (indicators.IntervalLinesVisible)
				vertical.AddRange(TimeBoundaryLines(loadedBars, indicators.IntervalHours, "interval"));

			var diagonal = new List<ChartDiagonalLine>();
			if (indicators.RegressionVisible && RegressionLine(windowBars) is { } regression)
				diagonal.Add(regression);

			var vwap = indicators.VwapVisible ? Vwap(windowBars) : new List<ChartLinePoint>();
			return new IndicatorChartOverlays(horizontal, vertical, diagonal, vwap);
		}

		private static List<ChartHorizontalLine> ProfileLevelLines(IReadOnlyList<WeightedPriceSample> samples, bool showPoc, bool showVah, bool showVal)
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

		private static List<ChartHorizontalLine> AverageMedianLines(IReadOnlyList<WeightedPriceSample> samples, bool showAverage, bool showMedian)
		{
			var lines = new List<ChartHorizontalLine>();
			if (showAverage && VolumeProfileStats.ComputeAverage(samples) is { } average)
				lines.Add(new ChartHorizontalLine(average, "AVG", "average"));
			if (showMedian && VolumeProfileStats.ComputeMedian(samples) is { } median)
				lines.Add(new ChartHorizontalLine(median, "MED", "median"));
			return lines;
		}

		private static List<ChartLinePoint> Vwap(IReadOnlyList<HlocAvdtBar> bars) =>
			VectorStatsHelper
				.Vwap(bars, b => b.OpenTime, b => b.Average, b => b.Volume)
				.Select(p => new ChartLinePoint(p.TimeUtc, p.Value))
				.ToList();

		// Close prices match XGrid's REG level, so the line's right end sits on the row XGrid highlights.
		private static ChartDiagonalLine? RegressionLine(IReadOnlyList<HlocAvdtBar> bars)
		{
			if (bars.Count < 2)
				return null;

			var (slope, intercept) = RangeStatsHelper.LinearFit(bars.Select(b => b.Close).ToList());
			return new ChartDiagonalLine(bars[0].OpenTime, intercept, bars[^1].OpenTime, slope * (bars.Count - 1) + intercept, "REG", "regression");
		}

		// Buckets are aligned to midnight, matching XGrid's day/interval columns.
		private static List<ChartVerticalLine> TimeBoundaryLines(IReadOnlyList<HlocAvdtBar> bars, int hours, string colorGroup)
		{
			var lines = new List<ChartVerticalLine>();
			if (hours <= 0)
				return lines;

			long BucketOf(DateTime t) => t.Ticks / (TimeSpan.TicksPerHour * hours);
			for (var i = 1; i < bars.Count; i++)
				if (BucketOf(bars[i].OpenTime) != BucketOf(bars[i - 1].OpenTime))
					lines.Add(new ChartVerticalLine(bars[i].OpenTime, null, colorGroup));
			return lines;
		}
	}
}
