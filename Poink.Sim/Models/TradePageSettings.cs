using System.Collections.Generic;

namespace Poink.Sim.Models
{
	/// <summary>
	/// Snapshot of the Trade page's layout, chart settings and watchlist, persisted to browser
	/// localStorage so it survives navigating away and back, and so the watchlist can be
	/// re-subscribed after an app restart empties CTraderSessionService's in-memory watchlist.
	/// </summary>
	internal sealed record TradePageSettings(
		int BarSizeSeconds,
		bool ShowMainChart,
		bool ShowXGrid,
		bool ShowVolumeProfile,
		bool ShowTimeProfile,
		bool ShowTraderControls,
		bool ShowProfileChartLines,
		bool ShowVwap,
		int BarWindowSize,
		int BarWindowOffset,
		int XGridPipCount,
		int XGridColCount,
		long? ChartSymbolId,
		string? ChartSymbolName,
		List<long> WatchedSymbolIds,
		bool SidepanelCollapsed
	);
}
