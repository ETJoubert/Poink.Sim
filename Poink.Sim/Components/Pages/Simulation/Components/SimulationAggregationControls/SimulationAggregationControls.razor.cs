using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Simulation
{
	/// <summary>
	/// The Simulation playback toolbar's bar aggregation controls: raw ticks versus aggregated bars, and the bar size.
	/// </summary>
	public partial class SimulationAggregationControls : ComponentBase
	{
		[Parameter, EditorRequired]
		public bool Aggregate { get; set; }

		[Parameter]
		public EventCallback<bool> AggregateChanged { get; set; }

		[Parameter, EditorRequired]
		public int BarSizeSeconds { get; set; }

		[Parameter]
		public EventCallback<int> BarSizeSecondsChanged { get; set; }

		[Parameter]
		public bool Disabled { get; set; }

		private Task OnAggregateChanged(ChangeEventArgs e) => AggregateChanged.InvokeAsync(e.Value is true);

		private Task OnBarSizeChanged(int seconds) => BarSizeSecondsChanged.InvokeAsync(seconds);
	}
}
