using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Shared.TraderSidePanel
{
	/// <summary>
	/// Tabbed shell for a page's trader-controls sidebar -- Trade/Policies/Robot -- shared by the live
	/// Trade page and the Simulation page so both present the same sidebar shape.
	/// </summary>
	public partial class TraderSidePanel : ComponentBase
	{
		private enum SidePanelTab
		{
			Trade,
			Policies,
			Robot,
		}

		private SidePanelTab _activeTab = SidePanelTab.Trade;

		/// <summary>The page-specific order-entry panel shown under the Trade tab.</summary>
		[Parameter]
		public RenderFragment? TradeTab { get; set; }
	}
}
