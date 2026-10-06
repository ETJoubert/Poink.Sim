using System.Collections.Generic;
using Poink.Sim.Components.Charts;
using Poink.Sim.Components.Pages.Trade.Models;
using Poink.Sim.Components.Shared.Indicators;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>TradeChartPanel's persisted toggle/window/XGrid/indicator/compare state -- round-tripped through TradePage's localStorage settings blob via CurrentSettings/InitialSettings, and through the popout URL.</summary>
	public sealed record ChartPanelSettings(
		bool ShowMainChart,
		bool ShowXGrid,
		bool ShowVolumeProfile,
		bool ShowTimeProfile,
		int BarWindowSize,
		int BarWindowOffset,
		int XGridPipCount,
		int XGridColCount,
		IndicatorSettings Indicators,
		bool ShowTxFold,
		IReadOnlyList<int> TxFoldSeconds,
		bool ShowCompare,
		TradeCompareSettings Compare
	)
	{
		public static ChartPanelSettings Default { get; } =
			new(
				ShowMainChart: true,
				ShowXGrid: false,
				ShowVolumeProfile: false,
				ShowTimeProfile: false,
				BarWindowSize: 50,
				BarWindowOffset: 0,
				XGridPipCount: 20,
				XGridColCount: 20,
				Indicators: new IndicatorSettings { ShowPoc = true, ShowVah = true, ShowVal = true, ShowVwap = true },
				ShowTxFold: false,
				TxFoldSeconds: [.. SnapshotTimeframeOptions.DefaultSelectedSeconds],
				ShowCompare: false,
				Compare: new TradeCompareSettings()
			);
	}
}
