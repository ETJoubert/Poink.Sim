using Poink.Sim.Components.Charts;

namespace Poink.Sim.Components.Pages.Trade.Models
{
	/// <summary>The Trade chart's compare view options, persisted with the rest of the chart panel settings.</summary>
	public sealed record TradeCompareSettings(
		CompareSetMode Mode = CompareSetMode.PercentChange,
		CompareSetAnchor Anchor = CompareSetAnchor.WindowStart,
		CompareChartSeriesStyle Style = CompareChartSeriesStyle.Lines,
		string? RatioTable = null,
		CompareSetTxFoldSettings? TxFold = null,
		CompareSetXGridSettings? XGrid = null,
		CompareSetPanel MainPanel = CompareSetPanel.Chart
	);
}
