using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>Builds the query string TradeChartPopoutPage reads back via [SupplyParameterFromQuery], so
	/// a popped-out chart opens already showing the same symbol, bar size, and panel settings as the
	/// panel it was popped out from. The settings travel as one JSON value so new panel options need no new query keys.</summary>
	internal static class ChartPopoutUrlHelper
	{
		public static string BuildUrl(long symbolId, string? symbolName, int barSizeSeconds, ChartPanelSettings settings)
		{
			var query = new Dictionary<string, string?>
			{
				["symbolId"] = symbolId.ToString(CultureInfo.InvariantCulture),
				["symbolName"] = symbolName,
				["barSizeSeconds"] = barSizeSeconds.ToString(CultureInfo.InvariantCulture),
				["settings"] = JsonSerializer.Serialize(settings),
			};

			return QueryHelpers.AddQueryString("/trade/popout/chart", query);
		}

		/// <summary>Reads the settings back, falling back to the defaults for a missing or unreadable value.</summary>
		public static ChartPanelSettings ParseSettings(string? json)
		{
			if (string.IsNullOrWhiteSpace(json))
				return ChartPanelSettings.Default;

			try
			{
				return JsonSerializer.Deserialize<ChartPanelSettings>(json) ?? ChartPanelSettings.Default;
			}
			catch (JsonException)
			{
				return ChartPanelSettings.Default;
			}
		}
	}
}
