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

		[Parameter, SupplyParameterFromQuery(Name = "showMainChart")]
		public bool ShowMainChart { get; set; } = true;

		[Parameter, SupplyParameterFromQuery(Name = "showXGrid")]
		public bool ShowXGrid { get; set; }

		[Parameter, SupplyParameterFromQuery(Name = "showVolumeProfile")]
		public bool ShowVolumeProfile { get; set; }

		[Parameter, SupplyParameterFromQuery(Name = "showTimeProfile")]
		public bool ShowTimeProfile { get; set; }

		[Parameter, SupplyParameterFromQuery(Name = "showProfileChartLines")]
		public bool ShowProfileChartLines { get; set; } = true;

		[Parameter, SupplyParameterFromQuery(Name = "showVwap")]
		public bool ShowVwap { get; set; } = true;

		[Parameter, SupplyParameterFromQuery(Name = "barWindowSize")]
		public int BarWindowSize { get; set; } = 50;

		[Parameter, SupplyParameterFromQuery(Name = "barWindowOffset")]
		public int BarWindowOffset { get; set; }

		[Parameter, SupplyParameterFromQuery(Name = "xgridPipCount")]
		public int XGridPipCount { get; set; } = 20;

		[Parameter, SupplyParameterFromQuery(Name = "xgridColCount")]
		public int XGridColCount { get; set; } = 20;

		private ChartPanelSettings InitialSettings =>
			new(
				ShowMainChart,
				ShowXGrid,
				ShowVolumeProfile,
				ShowTimeProfile,
				ShowProfileChartLines,
				ShowVwap,
				BarWindowSize,
				BarWindowOffset,
				XGridPipCount,
				XGridColCount
			);
	}
}
