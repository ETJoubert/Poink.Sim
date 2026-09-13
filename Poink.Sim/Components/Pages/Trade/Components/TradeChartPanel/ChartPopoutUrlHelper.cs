using System.Collections.Generic;
using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>Builds the query string TradeChartPopoutPage reads back via [SupplyParameterFromQuery], so
	/// a popped-out chart opens already showing the same symbol, bar size, and panel settings as the
	/// panel it was popped out from.</summary>
	internal static class ChartPopoutUrlHelper
	{
		public static string BuildUrl(long symbolId, string? symbolName, int barSizeSeconds, ChartPanelSettings settings)
		{
			var query = new Dictionary<string, string?>
			{
				["symbolId"] = symbolId.ToString(CultureInfo.InvariantCulture),
				["symbolName"] = symbolName,
				["barSizeSeconds"] = barSizeSeconds.ToString(CultureInfo.InvariantCulture),
				["showMainChart"] = settings.ShowMainChart.ToString(),
				["showXGrid"] = settings.ShowXGrid.ToString(),
				["showVolumeProfile"] = settings.ShowVolumeProfile.ToString(),
				["showTimeProfile"] = settings.ShowTimeProfile.ToString(),
				["showProfileChartLines"] = settings.ShowProfileChartLines.ToString(),
				["showVwap"] = settings.ShowVwap.ToString(),
				["barWindowSize"] = settings.BarWindowSize.ToString(CultureInfo.InvariantCulture),
				["barWindowOffset"] = settings.BarWindowOffset.ToString(CultureInfo.InvariantCulture),
				["xgridPipCount"] = settings.XGridPipCount.ToString(CultureInfo.InvariantCulture),
				["xgridColCount"] = settings.XGridColCount.ToString(CultureInfo.InvariantCulture),
			};

			return QueryHelpers.AddQueryString("/trade/popout/chart", query);
		}
	}
}
