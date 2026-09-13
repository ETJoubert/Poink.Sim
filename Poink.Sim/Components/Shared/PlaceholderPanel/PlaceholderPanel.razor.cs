using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Shared.PlaceholderPanel
{
	/// <summary>Stand-in for a tab or panel whose real content isn't built yet.</summary>
	public partial class PlaceholderPanel : ComponentBase
	{
		[Parameter, EditorRequired]
		public string Title { get; set; } = "";

		[Parameter]
		public string Message { get; set; } = "Coming soon.";
	}
}
