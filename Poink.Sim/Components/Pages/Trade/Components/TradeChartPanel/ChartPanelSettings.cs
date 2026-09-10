namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>TradeChartPanel's persisted toggle/window/XGrid state -- round-tripped through TradePage's localStorage settings blob via CurrentSettings/InitialSettings.</summary>
	public sealed record ChartPanelSettings(
		bool ShowMainChart,
		bool ShowXGrid,
		bool ShowVolumeProfile,
		bool ShowTimeProfile,
		bool ShowProfileChartLines,
		bool ShowVwap,
		int BarWindowSize,
		int BarWindowOffset,
		int XGridPipCount,
		int XGridColCount
	);
}
