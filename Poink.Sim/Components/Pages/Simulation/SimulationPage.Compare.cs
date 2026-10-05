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
		private List<SimulationCompareSymbol> _compareSymbols = new();

		private static bool CompareAvailable(SimulationSnapshot s) => s.Config.Aggregate;

		private bool CompareActive(SimulationSnapshot s) => _showCompare && CompareAvailable(s);

		// Every symbol is compared over its live bars only, so all lines cover the same span; the traded symbol's
		// backfilled history stays on its own chart.
		private IReadOnlyList<SimulationCompareSymbol> CompareSymbols(SimulationSnapshot s)
		{
			var watched = WatchedSymbols(s);
			var colors = ProfilerSymbolColorHelper.Assign([s.Config.Table, .. watched.Select(w => w.Table)]);
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

		/// <summary>The Lookback Window's edges over the live bars, shaded on the compare chart and used as its anchors.</summary>
		private (DateTime? Start, DateTime? End) CompareWindow(SimulationSnapshot s)
		{
			var (bars, _) = WindowedBarData(s);
			if (bars.Count == 0)
				return (null, null);
			return (bars[Math.Max(0, bars.Count - ClampedBarWindow)].OpenTime, bars[^1].OpenTime);
		}
	}
}
