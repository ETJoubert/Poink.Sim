using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Charts;
using Poink.Sim.Components.Shared.Indicators;
using Poink.Sim.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Components.Simulation
{
	/// <summary>
	/// Computes a watched symbol's chart set for the traded symbol's Lookback Window, applied by time so every symbol covers
	/// the same span the way the Profiler aligns its symbols; recomputes only when the symbol's bars or a setting changed.
	/// </summary>
	internal sealed class SimulationSymbolPanelBuilder
	{
		private sealed record Key(
			IReadOnlyList<HlocAvdtBar> Bars,
			IReadOnlyList<Tick> Ticks,
			string ColorVar,
			double PipSize,
			bool Aggregate,
			DateTime? Start,
			DateTime? End,
			IndicatorSettings Indicators,
			bool XGrid,
			int XGridPipCount,
			int XGridColCount,
			bool TimeProfile
		);

		private readonly SimulationRowCache _rowCache;
		private readonly Dictionary<string, (Key Key, SimulationSymbolPanelData Data)> _built = new();

		public SimulationSymbolPanelBuilder(SimulationRowCache rowCache) => _rowCache = rowCache;

		public SimulationSymbolPanelData Build(
			WatchedSymbolSnapshot symbol,
			string colorVar,
			double pipSize,
			bool aggregate,
			(DateTime? Start, DateTime? End) window,
			IndicatorSettings indicators,
			bool xgrid,
			int xgridPipCount,
			int xgridColCount,
			bool timeProfile
		)
		{
			var key = new Key(
				symbol.RecentBars,
				symbol.RecentTicks,
				colorVar,
				pipSize,
				aggregate,
				window.Start,
				window.End,
				indicators,
				xgrid,
				xgridPipCount,
				xgridColCount,
				timeProfile
			);
			if (_built.TryGetValue(symbol.Table, out var built) && built.Key == key)
				return built.Data;

			var data = aggregate
				? BuildAggregated(symbol, colorVar, pipSize, window, indicators, xgrid, xgridPipCount, xgridColCount, timeProfile)
				: BuildTicks(symbol, colorVar, pipSize);
			_built[symbol.Table] = (key, data);
			return data;
		}

		/// <summary>Index range of the bars opening inside [start, end]; a null edge leaves that side open.</summary>
		public static (int First, int Last) WindowRange(IReadOnlyList<HlocAvdtBar> bars, DateTime? start, DateTime? end)
		{
			var last = bars.Count - 1;
			if (end is { } e)
				while (last >= 0 && bars[last].OpenTime > e)
					last--;
			var first = 0;
			if (start is { } s)
				while (first <= last && bars[first].OpenTime < s)
					first++;
			return (first, last);
		}

		private SimulationSymbolPanelData BuildAggregated(
			WatchedSymbolSnapshot symbol,
			string colorVar,
			double pipSize,
			(DateTime? Start, DateTime? End) window,
			IndicatorSettings indicators,
			bool xgrid,
			int xgridPipCount,
			int xgridColCount,
			bool timeProfile
		)
		{
			var bars = symbol.RecentBars;
			var histograms = symbol.RecentBarHistograms;
			var (first, last) = WindowRange(bars, window.Start, window.End);
			last = Math.Min(last, histograms.Count - 1);
			var windowCount = Math.Max(0, last - first + 1);

			var barsToEnd = bars.Take(last + 1).ToList();
			var histogramsToEnd = histograms.Take(last + 1).ToList();
			var windowBars = barsToEnd.Skip(first).ToList();

			var samples = BarPriceHistogram
				.Merge(histogramsToEnd, windowCount)
				.Select(t => new WeightedPriceSample(t.Price, t.Count, t.UpCount, t.DownCount))
				.ToList();
			var timeSamples = timeProfile
				? BarPriceHistogram
					.MergeTime(barsToEnd, histogramsToEnd, windowCount)
					.Select(t => new WeightedTimeSample(t.Price, t.Count, t.Seconds, t.UpSeconds, t.DownSeconds))
					.ToList()
				: new List<WeightedTimeSample>();
			var grid = xgrid && windowCount > 0
				? XGridStats.Compute(
					barsToEnd,
					histogramsToEnd,
					samples,
					windowBars[0].OpenTime,
					windowBars[^1].OpenTime,
					xgridPipCount * pipSize,
					xgridColCount
				)
				: null;
			var overlays = IndicatorChartOverlayHelper.Build(indicators, bars, windowBars, samples);
			HlocAvdtBar? anchorBar = windowCount > 0 ? windowBars[^1] : null;

			return new SimulationSymbolPanelData(
				symbol.Table,
				colorVar,
				pipSize,
				symbol.CurrentTimestampUtc,
				symbol.CurrentBid,
				symbol.CurrentAsk,
				_rowCache.BarRows(symbol.Table, bars),
				new List<object>(),
				overlays.HorizontalLines,
				overlays.VerticalLines,
				overlays.DiagonalLines,
				overlays.VwapLine,
				windowCount > 0 ? windowBars[0].OpenTime : null,
				windowCount > 0 ? windowBars[^1].OpenTime : null,
				windowCount,
				samples,
				timeSamples,
				grid,
				grid is null ? null : TickRateHelper.Measure(symbol.RecentTicks, TickRateHelper.DefaultWindow),
				anchorBar?.CloseTime.AddTicks(1),
				anchorBar?.Close
			);
		}

		private SimulationSymbolPanelData BuildTicks(WatchedSymbolSnapshot symbol, string colorVar, double pipSize) =>
			new(
				symbol.Table,
				colorVar,
				pipSize,
				symbol.CurrentTimestampUtc,
				symbol.CurrentBid,
				symbol.CurrentAsk,
				new List<CTraderBarRow>(),
				_rowCache.TickRows(symbol.Table, symbol.RecentTicks),
				[],
				[],
				[],
				[],
				null,
				null,
				0,
				new List<WeightedPriceSample>(),
				new List<WeightedTimeSample>(),
				null,
				null,
				null,
				null
			);
	}
}
