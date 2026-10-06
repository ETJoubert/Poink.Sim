using Microsoft.AspNetCore.Components;
using Poink.Sim.Emulator;
using Poink.Sim.Helpers;

namespace Poink.Sim.Components.Emulator
{
	/// <summary>
	/// The price chart and the order book ladder side by side, fitted to one price scale so a price on the chart lines up
	/// with its row in the book.
	/// </summary>
	public partial class EmulatorMarketComponent : ComponentBase
	{
		[Parameter, EditorRequired]
		public EmulatorSnapshot Snapshot { get; set; } = default!;

		[Parameter]
		public double TickSize { get; set; } = 0.01;

		private EmulatorMarketScale _scale = default!;

		protected override void OnParametersSet()
		{
			_scale = EmulatorMarketScaleHelper.ScaleOf(Snapshot.History, Snapshot.Price, Snapshot.Book, TickSize);
		}
	}
}
