using System.Collections.Generic;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Profiler
{
	/// <summary>What the Profiler's compare chart measures each symbol's % change from.</summary>
	public enum ProfilerCompareMode
	{
		/// <summary>Each symbol's first bar in view, re-basing as the chart pans.</summary>
		PercentChange,

		/// <summary>Each symbol's bar at the Lookback Window edge picked by <see cref="ProfilerCompareAnchor"/>.</summary>
		WindowAnchored,
	}

	/// <summary>Which Lookback Window edge <see cref="ProfilerCompareMode.WindowAnchored"/> measures % change from.</summary>
	public enum ProfilerCompareAnchor
	{
		/// <summary>The window's left edge, so lines fan out from 0% across the window.</summary>
		WindowStart,

		/// <summary>The window's right edge, the same point TxFold anchors at, so lines converge on 0% there.</summary>
		WindowEnd,
	}

	/// <summary>The compare view's XGrid panel options; a null <paramref name="DifferenceTable"/> compares against the second symbol.</summary>
	public sealed record ProfilerCompareXGridSettings(
		bool Show = false,
		CompareXGridMode Mode = CompareXGridMode.Dominant,
		string? DifferenceTable = null,
		double RowPercent = 0.05,
		int ColCount = 20,
		bool ShowProfile = false,
		int TopCellsPercent = 0,
		int LowCellsPercent = 0
	);

	/// <summary>The compare view's TxFold panel options; a null <paramref name="Seconds"/> means the default timeframes.</summary>
	public sealed record ProfilerCompareTxFoldSettings(
		bool Show = false,
		CompareTxFoldLayout Layout = CompareTxFoldLayout.Overlaid,
		List<int>? Seconds = null,
		BarAggregationKind BarKind = BarAggregationKind.Ohlc,
		bool ShowVolumeProfile = false
	);

	/// <summary>Which compare view panel fills the main area; the others stack in the side column.</summary>
	public enum ProfilerComparePanel
	{
		Chart,
		TxFold,
		XGrid,
	}
}
