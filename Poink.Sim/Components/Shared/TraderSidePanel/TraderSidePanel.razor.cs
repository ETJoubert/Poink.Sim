using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Shared.TraderSidePanel
{
	/// <summary>
	/// Tabbed shell for a page's trader-controls sidebar -- Trade/Policies/Robot -- shared by the live
	/// Trade page and the Simulation page so both present the same sidebar shape. The collapse/expand
	/// toggle itself stays owned by the host page (its header carries a page-specific label), so this
	/// only reacts to Collapsed to swap its own tabs+content for an icon-only strip.
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

		[Parameter]
		public bool Collapsed { get; set; }

		/// <summary>Raised when a collapsed icon is clicked, so the host page can expand the panel.</summary>
		[Parameter]
		public EventCallback OnExpandRequested { get; set; }

		private Task SelectTabAndExpand(SidePanelTab tab)
		{
			_activeTab = tab;
			return OnExpandRequested.InvokeAsync();
		}
	}
}
