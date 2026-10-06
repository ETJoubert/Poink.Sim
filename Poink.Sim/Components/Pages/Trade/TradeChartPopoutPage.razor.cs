using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>Bare host for a TradeChartPanel popped out into its own OS window via PopoutButton --
	/// no page chrome (see PopoutLayout), initial state read entirely from the query string
	/// ChartPopoutUrlHelper built on the panel it was popped out from.</summary>
	public partial class TradeChartPopoutPage : ComponentBase
	{
		[Parameter, SupplyParameterFromQuery]
		public long? SymbolId { get; set; }

		[Parameter, SupplyParameterFromQuery]
		public string? SymbolName { get; set; }

		[Parameter, SupplyParameterFromQuery]
		public int BarSizeSeconds { get; set; } = 60;

		[Parameter, SupplyParameterFromQuery(Name = "settings")]
		public string? SettingsJson { get; set; }

		private ChartPanelSettings InitialSettings => ChartPopoutUrlHelper.ParseSettings(SettingsJson);
	}
}
