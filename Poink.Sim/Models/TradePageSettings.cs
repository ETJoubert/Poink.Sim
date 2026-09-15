namespace Poink.Sim.Models
{
	/// <summary>
	/// Snapshot of the Trade page's layout and chart settings, persisted to browser localStorage so
	/// it survives navigating away and back. The watchlist is persisted separately, per account --
	/// see TradeCoordinator's account-scoped watchlist storage.
	/// </summary>
	internal sealed record TradePageSettings(
		int BarSizeSeconds,
		bool ShowMainChart,
		bool ShowXGrid,
		bool ShowVolumeProfile,
		bool ShowTimeProfile,
		bool ShowProfileChartLines,
		bool ShowVwap,
		int BarWindowSize,
		int BarWindowOffset,
		int XGridPipCount,
		int XGridColCount,
		long? ChartSymbolId,
		string? ChartSymbolName,
		bool SidepanelCollapsed,
		bool RightPanelCollapsed
	);
}
