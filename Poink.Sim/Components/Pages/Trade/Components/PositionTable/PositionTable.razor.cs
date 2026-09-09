using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Services.CTrader;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>Read-only, tight-row table of open positions for the live Trade page -- no card view or column picker, unlike the generic DataTable.</summary>
	public partial class PositionTable : ComponentBase
	{
		[Parameter, EditorRequired]
		public IReadOnlyList<CTraderPositionInfo> Positions { get; set; } = System.Array.Empty<CTraderPositionInfo>();

		[Parameter]
		public EventCallback<CTraderPositionInfo> OnSelect { get; set; }

		private Task SelectAsync(CTraderPositionInfo position) => OnSelect.InvokeAsync(position);
	}
}
