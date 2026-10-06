using System.Linq;
using Poink.Sim.Components.Pages.Trade.Models;
using Poink.Sim.Components.Shared.Indicators;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Pages.Trade.Helpers
{
	/// <summary>Maps the Trade chart panel's settings to and from the persisted page settings blob, including blobs saved before Indicators existed.</summary>
	internal static class TradeChartSettingsHelper
	{
		public static ChartPanelSettings FromPersisted(TradePageSettings settings)
		{
			var defaults = ChartPanelSettings.Default;
			return new ChartPanelSettings(
				settings.ShowMainChart,
				settings.ShowXGrid,
				settings.ShowVolumeProfile,
				settings.ShowTimeProfile,
				settings.BarWindowSize,
				settings.BarWindowOffset,
				settings.XGridPipCount,
				settings.XGridColCount,
				settings.Indicators ?? LegacyIndicators(settings.ShowProfileChartLines, settings.ShowVwap),
				settings.ShowTxFold,
				settings.TxFoldSeconds is { Count: > 0 } seconds ? seconds : defaults.TxFoldSeconds,
				settings.ShowCompare,
				settings.Compare ?? defaults.Compare
			);
		}

		public static TradePageSettings ToPersisted(
			ChartPanelSettings? chart,
			int barSizeSeconds,
			long? chartSymbolId,
			string? chartSymbolName,
			bool sidepanelCollapsed,
			bool rightPanelCollapsed
		)
		{
			var c = chart ?? ChartPanelSettings.Default;
			return new TradePageSettings(
				barSizeSeconds,
				c.ShowMainChart,
				c.ShowXGrid,
				c.ShowVolumeProfile,
				c.ShowTimeProfile,
				c.Indicators.ShowPoc,
				c.Indicators.ShowVwap,
				c.BarWindowSize,
				c.BarWindowOffset,
				c.XGridPipCount,
				c.XGridColCount,
				chartSymbolId,
				chartSymbolName,
				sidepanelCollapsed,
				rightPanelCollapsed,
				c.Indicators,
				c.ShowTxFold,
				c.TxFoldSeconds.ToList(),
				c.ShowCompare,
				c.Compare
			);
		}

		private static IndicatorSettings LegacyIndicators(bool showProfileLines, bool showVwap) =>
			new()
			{
				ShowPoc = showProfileLines,
				ShowVah = showProfileLines,
				ShowVal = showProfileLines,
				ShowVwap = showVwap,
			};
	}
}
