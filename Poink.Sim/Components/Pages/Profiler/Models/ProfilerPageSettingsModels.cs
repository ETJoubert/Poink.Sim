using System;
using System.Collections.Generic;
using Poink.Sim.Components.Shared.Indicators;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Profiler
{
	/// <summary>
	/// Snapshot of the Profiler page's options (range, symbols, aggregation, panels, indicators, Lookback Window and
	/// chart set heights), persisted to browser localStorage so it survives reloading or navigating away and back.
	/// </summary>
	internal sealed record ProfilerPageSettings(
		DateTime From,
		DateTime To,
		List<string>? SelectedTables,
		int BarSizeSeconds,
		BarAggregationKind BarAggregationKind,
		IndicatorSettings? Indicators,
		bool LookbackEnabled,
		int LookbackWidth,
		int LookbackOffset,
		bool ShowMainChart,
		bool ShowXGrid,
		bool ShowVolumeProfile,
		bool ShowTimeProfile,
		bool ShowSubchart,
		bool ShowSnapshot,
		int SubchartBarSizeSeconds,
		BarAggregationKind SubchartBarKind,
		int XGridColCount,
		bool RightPanelCollapsed,
		Dictionary<string, double>? SetHeights
	)
	{
		/// <summary>The browser localStorage key the Profiler page keeps these settings under.</summary>
		public const string StorageKey = "poink.profiler.settings";
	}
}
