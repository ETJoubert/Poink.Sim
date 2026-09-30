using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Charts;
using Poink.Sim.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Services.CTrader;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>Pure lookback-window math for TradeChartPanel -- windowing, VWAP, profile overlay lines and XGrid position mapping, all derived from the same offset/width slider (see TradeChartPanel.razor.cs's ClampedBarWindow/ClampedBarOffset). Internal because it takes BarPriceHistogram, which is itself internal.</summary>
	internal static class ChartWindowHelper
	{
		public static (IReadOnlyList<HlocAvdtBar> Bars, IReadOnlyList<BarPriceHistogram> Histograms) WindowedBarData(
			IReadOnlyList<HlocAvdtBar> hlocBars,
			IReadOnlyList<BarPriceHistogram> histograms,
			int clampedOffset
		)
		{
			var offset = Math.Min(clampedOffset, Math.Max(0, hlocBars.Count - 1));
			var count = hlocBars.Count - offset;
			if (count <= 0)
				return (Array.Empty<HlocAvdtBar>(), Array.Empty<BarPriceHistogram>());
			return (hlocBars.Take(count).ToList(), histograms.Take(count).ToList());
		}

		public static List<WeightedPriceSample> MergedBarSamples(
			IReadOnlyList<HlocAvdtBar> hlocBars,
			IReadOnlyList<BarPriceHistogram> histograms,
			int clampedOffset,
			int clampedWindow
		)
		{
			var (_, windowedHistograms) = WindowedBarData(hlocBars, histograms, clampedOffset);
			return BarPriceHistogram
				.Merge(windowedHistograms, clampedWindow)
				.Select(t => new WeightedPriceSample(t.Price, t.Count, t.UpCount, t.DownCount))
				.ToList();
		}

		public static List<WeightedTimeSample> MergedBarTimeSamples(
			IReadOnlyList<HlocAvdtBar> hlocBars,
			IReadOnlyList<BarPriceHistogram> histograms,
			int clampedOffset,
			int clampedWindow
		)
		{
			var (bars, windowedHistograms) = WindowedBarData(hlocBars, histograms, clampedOffset);
			return BarPriceHistogram
				.MergeTime(bars, windowedHistograms, clampedWindow)
				.Select(t => new WeightedTimeSample(t.Price, t.Count, t.Seconds, t.UpSeconds, t.DownSeconds))
				.ToList();
		}

		public static List<XGridPosition> XGridPositions(IReadOnlyList<CTraderPositionInfo> positions, long? symbolId) =>
			positions
				.Where(p => p.SymbolId == symbolId)
				.Select(p => new XGridPosition(
					p.IsLong ? XGridPositionDirection.Long : XGridPositionDirection.Short,
					p.OpenTimeUtc,
					p.EntryPrice,
					StopLoss: p.StopLossPrice,
					TakeProfit: p.TakeProfitPrice
				)
				{
					Id = p.PositionId.ToString(),
				})
				.ToList();

		public static List<ChartHorizontalLine> PositionLines(IReadOnlyList<CTraderPositionInfo> positions, long? symbolId) =>
			positions
				.Where(p => p.SymbolId == symbolId)
				.Select(p => new ChartHorizontalLine(p.EntryPrice, $"{(p.IsLong ? "LONG" : "SHORT")} {p.EntryPrice:N5}", "position"))
				.ToList();

		public static List<ChartHorizontalLine> DraftLines(TradeDraft? draft, long? symbolId)
		{
			if (draft is not { } d || d.SymbolId != symbolId)
				return new();

			var lines = new List<ChartHorizontalLine> { new(d.EntryPrice, $"DRAFT {(d.IsLong ? "LONG" : "SHORT")}", "draft") };
			if (d.StopLossPrice is { } sl)
				lines.Add(new ChartHorizontalLine(sl, "DRAFT SL", "draft"));
			if (d.TakeProfitPrice is { } tp)
				lines.Add(new ChartHorizontalLine(tp, "DRAFT TP", "draft"));
			return lines;
		}

		public static List<ChartHorizontalLine> ProfileLevelLines(bool showProfileChartLines, List<WeightedPriceSample> mergedSamples)
		{
			if (!showProfileChartLines)
				return new();
			if (VolumeProfileStats.Compute(mergedSamples) is not { } levels)
				return new();
			return new()
			{
				new ChartHorizontalLine(levels.Poc, "POC", "profile"),
				new ChartHorizontalLine(levels.ValueAreaHigh, "VAH", "profile"),
				new ChartHorizontalLine(levels.ValueAreaLow, "VAL", "profile"),
			};
		}

		public static List<ChartLinePoint> VwapPoints(
			bool showVwap,
			IReadOnlyList<HlocAvdtBar> hlocBars,
			IReadOnlyList<BarPriceHistogram> histograms,
			int clampedOffset,
			int clampedWindow
		)
		{
			if (!showVwap)
				return new();

			var (bars, _) = WindowedBarData(hlocBars, histograms, clampedOffset);
			var windowBars = bars.Count > clampedWindow ? bars.Skip(bars.Count - clampedWindow) : bars;

			return VectorStatsHelper
				.Vwap(windowBars, b => b.OpenTime, b => b.Average, b => b.Volume)
				.Select(p => new ChartLinePoint(p.TimeUtc, p.Value))
				.ToList();
		}

		public static DateTime? WindowStartUtc(
			IReadOnlyList<HlocAvdtBar> hlocBars,
			IReadOnlyList<BarPriceHistogram> histograms,
			int clampedOffset,
			int clampedWindow
		)
		{
			var (bars, _) = WindowedBarData(hlocBars, histograms, clampedOffset);
			if (bars.Count <= clampedWindow)
				return null;
			return bars[bars.Count - clampedWindow].OpenTime;
		}

		// Only meaningful once Offset pulls the window's far edge off of "now" -- at Offset 0
		// the end is just the newest bar, already the chart's own right edge.
		public static DateTime? WindowEndUtc(
			IReadOnlyList<HlocAvdtBar> hlocBars,
			IReadOnlyList<BarPriceHistogram> histograms,
			int clampedOffset
		)
		{
			if (clampedOffset <= 0)
				return null;
			var (bars, _) = WindowedBarData(hlocBars, histograms, clampedOffset);
			return bars.Count > 0 ? bars[^1].OpenTime : null;
		}
	}
}
