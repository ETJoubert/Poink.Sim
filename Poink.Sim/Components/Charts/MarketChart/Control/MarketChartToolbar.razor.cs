using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Charts
{
	/// <summary>Draw-mode/expand toolbar for MarketChartContainer; purely presentational -- owns no state of its own.</summary>
	public partial class MarketChartToolbar : ComponentBase
	{
		[Parameter]
		public bool ShowAnnotationTools { get; set; } = true;

		[Parameter]
		public DrawMode Mode { get; set; }

		[Parameter]
		public string? DrawHint { get; set; }

		[Parameter]
		public bool HasVolumeRange { get; set; }

		[Parameter]
		public bool Expanded { get; set; }

		[Parameter]
		public EventCallback<DrawMode> OnModeToggled { get; set; }

		[Parameter]
		public EventCallback OnVolumeRangeToggled { get; set; }

		[Parameter]
		public EventCallback OnExpandedToggled { get; set; }
	}
}
