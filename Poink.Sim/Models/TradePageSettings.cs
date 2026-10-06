using System.Collections.Generic;
using Poink.Sim.Components.Pages.Trade.Models;
using Poink.Sim.Components.Shared.Indicators;

namespace Poink.Sim.Models
{
	/// <summary>
	/// Snapshot of the Trade page's layout and chart settings, persisted to browser localStorage so
	/// it survives navigating away and back. The watchlist is persisted separately, per account --
	/// see TradeCoordinator's account-scoped watchlist storage. ShowProfileChartLines/ShowVwap predate
	/// <see cref="Indicators"/> and seed it when a blob saved before it is loaded.
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
		bool RightPanelCollapsed,
		IndicatorSettings? Indicators = null,
		bool ShowTxFold = false,
		List<int>? TxFoldSeconds = null,
		bool ShowCompare = false,
		TradeCompareSettings? Compare = null
	);
}
