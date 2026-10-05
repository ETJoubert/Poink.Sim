using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Charts;
using Poink.Sim.Helpers;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Profiler
{
	/// <summary>
	/// Everything loaded for one Profiler symbol: its bars, quartile bars and per-bar price histograms, accumulated
	/// chunk by chunk as older history backfills, plus the chart-row projection for the active bar kind.
	/// </summary>
	public sealed class ProfilerSymbolData
	{
		private XGridResult? _xgridCache;
		private (int Pips, int Cols, DateTime Start, DateTime End, int Count, double LastClose)? _xgridKey;

		internal ProfilerSymbolData(string table) => Table = table;

		public string Table { get; }

		internal List<HlocAvdtBar> Bars { get; private set; } = new();

		internal List<QuartileBar> QuartileBars { get; private set; } = new();

		internal List<BarPriceHistogram> Histograms { get; private set; } = new();

		/// <summary>Reassigned only on data or bar-kind changes, since MarketChart skips its JS setData by reference equality.</summary>
		public List<CTraderBarRow> BarRows { get; private set; } = new();

		/// <summary>Per-bar median overlay, only populated in Quartile bar-kind mode.</summary>
		public List<ChartLinePoint> MedianLine { get; private set; } = new();

		public int TickCount { get; private set; }

		public bool ReachedStart { get; private set; }

		public bool IsLoading { get; internal set; }

		public bool IsLoadingMore { get; internal set; }

		/// <summary>Latched once older history is requested so later chunks don't re-fit the chart's view.</summary>
		public bool KeepView { get; internal set; }

		public string? Error { get; internal set; }

		public bool HasBars => Bars.Count > 0;

		public double? LastClose => Bars.Count > 0 ? Bars[^1].Close : null;

		public string SummaryLabel =>
			$"{Bars.Count:N0} bars / {TickCount:N0} ticks{(ReachedStart ? " (full range loaded)" : " (more history available)")}";

		internal void ApplyChunk(ProfilerChunk chunk, BarAggregationKind barKind)
		{
			Bars = chunk.Bars.Concat(Bars).ToList();
			QuartileBars = chunk.QuartileBars.Concat(QuartileBars).ToList();
			Histograms = chunk.BarHistograms.Concat(Histograms).ToList();
			TickCount += chunk.TickCount;
			ReachedStart = chunk.ReachedStart;
			RefreshBarRows(barKind);
		}

		// Volume/Time Profile, XGrid, and VWAP always stay wired to Bars/Histograms since those can only be computed from ticks.
		internal void RefreshBarRows(BarAggregationKind barKind)
		{
			if (barKind == BarAggregationKind.Quartile)
			{
				BarRows = ChartBarRowHelper.ToQuartileBarRows(QuartileBars);
				MedianLine = ChartBarRowHelper.ToMedianLine(QuartileBars);
			}
			else
			{
				BarRows = ChartBarRowHelper.ToBarRows(Bars);
				MedianLine = new();
			}
		}

		/// <summary>Resolves a window of <paramref name="width"/> bars ending <paramref name="offset"/> bars before the latest bar to its OpenTime bounds.</summary>
		internal (DateTime Start, DateTime End)? ResolveWindow(int width, int offset)
		{
			if (Bars.Count == 0)
				return null;

			var endIndex = Math.Clamp(Bars.Count - 1 - offset, 0, Bars.Count - 1);
			var startIndex = Math.Clamp(endIndex - width + 1, 0, endIndex);
			return (Bars[startIndex].OpenTime, Bars[endIndex].OpenTime);
		}

		/// <summary>The inverse of <see cref="ResolveWindow"/>: this symbol's own width/offset in bars for a time window, if any of its bars fall inside it.</summary>
		internal (int Width, int Offset)? WindowInBars(DateTime start, DateTime end)
		{
			var startIndex = Bars.FindIndex(b => b.OpenTime >= start);
			var endIndex = Bars.FindLastIndex(b => b.OpenTime <= end);
			if (startIndex < 0 || endIndex < 0 || endIndex < startIndex)
				return null;

			return (endIndex - startIndex + 1, Bars.Count - 1 - endIndex);
		}

		internal int BarCountIn(DateTime start, DateTime end) => Bars.Count(b => b.OpenTime >= start && b.OpenTime <= end);

		/// <summary>The last bar opening at or before <paramref name="end"/>, whose CloseTime/Close anchor the TxFold snapshot.</summary>
		internal HlocAvdtBar? BarAtOrBefore(DateTime end)
		{
			var index = Bars.FindLastIndex(b => b.OpenTime <= end);
			return index >= 0 ? Bars[index] : null;
		}

		internal XGridResult? XGrid(DateTime start, DateTime end, double bucketSize, int pipCount, int colCount)
		{
			var key = (pipCount, colCount, start, end, Bars.Count, LastClose ?? 0);
			if (_xgridKey == key && _xgridCache is not null)
				return _xgridCache;

			_xgridKey = key;
			_xgridCache = XGridStats.Compute(
				Bars,
				Histograms,
				ProfilerSymbolStatsHelper.MergedSamples(this, start, end),
				start,
				end,
				bucketSize,
				colCount
			);
			return _xgridCache;
		}
	}
}
