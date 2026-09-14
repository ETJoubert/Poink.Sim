using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Simulation
{
	/// <summary>
	/// One trader's live figures as a card, for the Simulation page's Traders tab -- click to select, same
	/// as the table's old row click did. Takes plain values rather than the internal TraderSnapshot itself,
	/// since a public component parameter can't expose an internal type.
	/// </summary>
	public partial class TraderCard : ComponentBase
	{
		[Parameter, EditorRequired]
		public string Label { get; set; } = "";

		[Parameter]
		public bool IsManual { get; set; }

		[Parameter]
		public string PositionStatus { get; set; } = "FLAT";

		[Parameter]
		public string PositionStatusCssClass { get; set; } = "tcard-status-flat";

		[Parameter]
		public int TradeCount { get; set; }

		[Parameter]
		public double Cash { get; set; }

		[Parameter]
		public double Equity { get; set; }

		[Parameter]
		public double RealizedPnl { get; set; }

		[Parameter]
		public double UnrealizedPnl { get; set; }

		[Parameter]
		public double MarginUsed { get; set; }

		[Parameter]
		public double? MarginLevel { get; set; }

		[Parameter]
		public bool IsSelected { get; set; }

		[Parameter]
		public EventCallback OnSelect { get; set; }

		private Task SelectAsync() => OnSelect.InvokeAsync();
	}
}
