using System.Collections.Generic;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Simulation
{
	/// <summary>What the Simulation compare chart measures each symbol's % change from.</summary>
	public enum SimulationCompareMode
	{
		/// <summary>Each symbol's first bar in view, re-basing as the chart pans.</summary>
		PercentChange,

		/// <summary>Each symbol's bar at the Lookback Window edge picked by <see cref="SimulationCompareAnchor"/>.</summary>
		WindowAnchored,
	}

	/// <summary>Which Lookback Window edge <see cref="SimulationCompareMode.WindowAnchored"/> measures % change from.</summary>
	public enum SimulationCompareAnchor
	{
		/// <summary>The window's left edge, so lines fan out from 0% across the window.</summary>
		WindowStart,

		/// <summary>The window's right edge, so lines converge on 0% there.</summary>
		WindowEnd,
	}

	/// <summary>One symbol on the Simulation compare chart, its bars oldest first; the traded symbol comes first.</summary>
	public sealed record SimulationCompareSymbol(string Table, string ColorVar, IReadOnlyList<CTraderBarRow> Bars);

	/// <summary>The compare view's TxFold panel options; a null <paramref name="Seconds"/> means the default timeframes.</summary>
	public sealed record SimulationCompareTxFoldSettings(
		bool Show = false,
		CompareTxFoldLayout Layout = CompareTxFoldLayout.Overlaid,
		List<int>? Seconds = null,
		BarAggregationKind BarKind = BarAggregationKind.Ohlc,
		bool ShowVolumeProfile = false
	);

	/// <summary>The compare view's XGrid panel options; a null <paramref name="DifferenceTable"/> compares against the second symbol.</summary>
	public sealed record SimulationCompareXGridSettings(
		bool Show = false,
		CompareXGridMode Mode = CompareXGridMode.Dominant,
		string? DifferenceTable = null,
		double RowPercent = 0.05,
		int ColCount = 20,
		bool ShowProfile = false,
		int TopCellsPercent = 0,
		int LowCellsPercent = 0
	);

	/// <summary>Which compare view panel fills the main area; the others stack in the side column.</summary>
	public enum SimulationComparePanel
	{
		Chart,
		TxFold,
		XGrid,
	}
}
