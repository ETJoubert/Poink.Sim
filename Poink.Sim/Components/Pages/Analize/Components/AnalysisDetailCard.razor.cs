using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Pages.Analize
{
	public partial class AnalysisDetailCard : ComponentBase
	{
		[Parameter]
		public AnalysisDetailView? Snapshot { get; set; }

		[Parameter]
		public string Title { get; set; } = "Details";

		[Parameter]
		public bool ShowLabelHeader { get; set; }

		[Parameter]
		public string? SelectedSeries { get; set; }

		[Parameter]
		public RenderFragment? Actions { get; set; }

		[Parameter]
		public bool Clickable { get; set; }

		[Parameter]
		public EventCallback OnCardClick { get; set; }

		[Parameter]
		public string EmptyMessage { get; set; } = "no completed run yet";

		private static string FormatFractionPercentText(double value) => (value * 100).ToString("N1");

		private string FormatToReturnValueText(double value) =>
			Snapshot?.ToReturnUnit == "bars" ? $"{(value + 1).ToString("N1")} bars" : $"{value.ToString("N1")} min";

		private Task HandleCardClick() => Clickable ? OnCardClick.InvokeAsync() : Task.CompletedTask;
	}
}
