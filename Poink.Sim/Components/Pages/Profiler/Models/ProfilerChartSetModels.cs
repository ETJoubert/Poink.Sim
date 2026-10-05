using System;

namespace Poink.Sim.Components.Profiler
{
	/// <summary>
	/// The Profiler toolbar's global panel, overlay and Lookback Window state, applied identically to every symbol's chart set.
	/// The window is held as bar OpenTimes so all sets cover the same slice of time; LookbackWidth is the toolbar's width in
	/// primary-symbol bars, used when a symbol has no bars of its own inside the window.
	/// </summary>
	public sealed record ProfilerChartSetOptions(
		bool ShowMainChart,
		bool ShowXGrid,
		bool ShowVolumeProfile,
		bool ShowTimeProfile,
		bool ShowSubchart,
		bool ShowSnapshot,
		bool ShowProfilePanelLevels,
		bool ShowProfileChartLines,
		bool ShowVwap,
		bool ShowAverageLine,
		bool ShowMedianLine,
		bool LookbackEnabled,
		DateTime? WindowStart,
		DateTime? WindowEnd,
		int LookbackWidth,
		DateTime RangeStart
	)
	{
		public bool HasWindow => WindowStart is not null && WindowEnd is not null;
	}
}
