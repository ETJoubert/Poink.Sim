using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Charts
{
	/// <summary>What the compare chart measures each symbol's % change from.</summary>
	public enum CompareSetMode
	{
		/// <summary>Each symbol's first bar in view, re-basing as the chart pans.</summary>
		PercentChange,

		/// <summary>Each symbol's bar at the Lookback Window edge picked by <see cref="CompareSetAnchor"/>.</summary>
		WindowAnchored,
	}

	/// <summary>Which Lookback Window edge <see cref="CompareSetMode.WindowAnchored"/> measures % change from.</summary>
	public enum CompareSetAnchor
	{
		/// <summary>The window's left edge, so lines fan out from 0% across the window.</summary>
		WindowStart,

		/// <summary>The window's right edge, so lines converge on 0% there.</summary>
		WindowEnd,
	}

	/// <summary>
	/// One symbol on the compare chart, its bars oldest first; the primary symbol comes first. <paramref name="HistoryTable"/>,
	/// when set, supplies TxFold's ticks before the set's HistoryBeforeUtc and <paramref name="Table"/> the rest.
	/// </summary>
	public sealed record CompareSetSymbol(string Table, string ColorVar, IReadOnlyList<CTraderBarRow> Bars, string? HistoryTable = null);

	/// <summary>The compare view's TxFold panel options; a null <paramref name="Seconds"/> means the default timeframes.</summary>
	public sealed record CompareSetTxFoldSettings(
		bool Show = false,
		CompareTxFoldLayout Layout = CompareTxFoldLayout.Overlaid,
		List<int>? Seconds = null,
		BarAggregationKind BarKind = BarAggregationKind.Ohlc,
		bool ShowVolumeProfile = false
	);

	/// <summary>The compare view's XGrid panel options; a null <paramref name="DifferenceTable"/> compares against the second symbol.</summary>
	public sealed record CompareSetXGridSettings(
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
	public enum CompareSetPanel
	{
		Chart,
		TxFold,
		XGrid,
	}
}
