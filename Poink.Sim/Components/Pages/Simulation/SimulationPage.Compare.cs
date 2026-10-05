using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Charts;
using Poink.Sim.Components.Profiler;
using Poink.Sim.Components.Simulation;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Pages
{
	/// <summary>
	/// The compare view that replaces the chart row and watched charts while on: the traded and watched symbols' live
	/// bars on one <see cref="CompareChart"/>, coloured with the Profiler's per-symbol palette.
	/// </summary>
	public partial class SimulationPage
	{
		private bool _showCompare;
		private SimulationCompareMode _compareMode;
		private SimulationCompareAnchor _compareAnchor;
		private CompareChartSeriesStyle _compareStyle;
		private string? _compareRatioTable;
		private SimulationCompareTxFoldSettings _compareTxFold = new();
		private SimulationCompareXGridSettings _compareXGrid = new();
		private SimulationComparePanel _compareMainPanel;
		private List<SimulationCompareSymbol> _compareSymbols = new();
		private IReadOnlyList<string> _symbolColorTables = [];
		private IReadOnlyDictionary<string, string>? _symbolColors;
		private CompareXGridResult? _compareXGridResult;
		private (DateTime Start, DateTime End, DateTime Anchor, double RowPercent, int ColCount, IReadOnlyDictionary<string, string> Colors)? _compareXGridKey;
		private List<IReadOnlyList<HlocAvdtBar>> _compareXGridBars = new();

		private static bool CompareAvailable(SimulationSnapshot s) => s.Config.Aggregate;

		private bool CompareActive(SimulationSnapshot s) => _showCompare && CompareAvailable(s);

		// Every symbol is compared over its live bars only, so all lines cover the same span; the traded symbol's
		// backfilled history stays on its own chart.
		private IReadOnlyList<SimulationCompareSymbol> CompareSymbols(SimulationSnapshot s)
		{
			var watched = WatchedSymbols(s);
			var colors = SymbolColors(s);
			var symbols = new List<SimulationCompareSymbol>(watched.Count + 1)
			{
				CompareSymbol(s.Config.Table, s.RecentBars, colors),
			};
			symbols.AddRange(watched.Select(w => CompareSymbol(w.Table, w.RecentBars, colors)));

			if (symbols.SequenceEqual(_compareSymbols))
				return _compareSymbols;
			_compareSymbols = symbols;
			return _compareSymbols;
		}

		private SimulationCompareSymbol CompareSymbol(
			string table,
			IReadOnlyList<HlocAvdtBar> bars,
			IReadOnlyDictionary<string, string> colors
		) => new(table, ProfilerSymbolColorHelper.ColorVarFor(colors, table), _rowCache.BarRows(table, bars));

		private IReadOnlyDictionary<string, string> SymbolColors(SimulationSnapshot s)
		{
			var tables = (IReadOnlyList<string>)[s.Config.Table, .. WatchedSymbols(s).Select(w => w.Table)];
			if (_symbolColors is null || !_symbolColorTables.SequenceEqual(tables))
			{
				_symbolColorTables = tables;
				_symbolColors = ProfilerSymbolColorHelper.Assign(tables);
			}
			return _symbolColors;
		}

		/// <summary>
		/// The traded symbol's Lookback Window edges as bar open times, so the compare view and every watched chart set cover
		/// the same span of time.
		/// </summary>
		private (DateTime? Start, DateTime? End) LookbackWindowTimes(SimulationSnapshot s)
		{
			var (bars, _) = WindowedBarData(s);
			if (bars.Count == 0)
				return (null, null);
			return (bars[Math.Max(0, bars.Count - ClampedBarWindow)].OpenTime, bars[^1].OpenTime);
		}

		// ITickRepository's "to" is exclusive, so +1 tick keeps the anchor bar's own closing tick inside every TxFold window.
		private DateTime? CompareTxFoldAnchor(SimulationSnapshot s) => SnapshotAnchorBar(s)?.CloseTime.AddTicks(1);

		// Always anchored at the picked window edge, since a grid has no "first bar in view" to re-base on.
		private CompareXGridResult? CompareXGrid(SimulationSnapshot s)
		{
			var (start, end) = LookbackWindowTimes(s);
			var anchor = _compareAnchor == SimulationCompareAnchor.WindowEnd ? end : start;
			if (!_compareXGrid.Show || start is not { } windowStart || end is not { } windowEnd || anchor is not { } anchorTime)
				return null;

			var sources = new List<(string Table, IReadOnlyList<HlocAvdtBar> Bars, IReadOnlyList<BarPriceHistogram> Histograms)>
			{
				(s.Config.Table, s.RecentBars, s.RecentBarHistograms),
			};
			sources.AddRange(WatchedSymbols(s).Select(w => (w.Table, w.RecentBars, w.RecentBarHistograms)));
			var colors = SymbolColors(s);
			var key = (windowStart, windowEnd, anchorTime, _compareXGrid.RowPercent, _compareXGrid.ColCount, colors);
			var bars = sources.Select(source => source.Bars).ToList();
			if (_compareXGridKey == key && _compareXGridBars.SequenceEqual(bars, ReferenceEqualityComparer.Instance))
				return _compareXGridResult;

			var inputs = sources
				.Select(source => (source, Reference: ReferenceClose(source.Bars, anchorTime, windowStart)))
				.Where(x => x.Reference is not null)
				.Select(x => new CompareXGridInput(
					x.source.Table,
					ProfilerSymbolColorHelper.ColorVarFor(colors, x.source.Table),
					x.source.Bars,
					x.source.Histograms,
					x.Reference!.Value
				))
				.ToList();
			_compareXGridKey = key;
			_compareXGridBars = bars;
			_compareXGridResult = CompareXGridStats.Compute(inputs, windowStart, windowEnd, _compareXGrid.RowPercent, _compareXGrid.ColCount);
			return _compareXGridResult;
		}

		// A symbol with no bar by the anchor (its data starts later) is measured from its first bar inside the window instead.
		private static double? ReferenceClose(IReadOnlyList<HlocAvdtBar> bars, DateTime anchor, DateTime windowStart)
		{
			var (_, atOrBefore) = SimulationSymbolPanelBuilder.WindowRange(bars, null, anchor);
			if (atOrBefore >= 0 && bars[atOrBefore].Close != 0)
				return bars[atOrBefore].Close;
			var (first, last) = SimulationSymbolPanelBuilder.WindowRange(bars, windowStart, null);
			return first <= last && bars[first].Close != 0 ? bars[first].Close : null;
		}
	}
}
