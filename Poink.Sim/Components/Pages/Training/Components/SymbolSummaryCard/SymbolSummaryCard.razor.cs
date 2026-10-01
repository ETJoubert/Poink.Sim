using Microsoft.AspNetCore.Components;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Pages.Training.Components.SymbolSummaryCard
{
	public partial class SymbolSummaryCard : ComponentBase
	{
		[Parameter]
		public SymbolModel? Symbol { get; set; }

		[Parameter]
		public SymbolMetadataModel? Metadata { get; set; }

		[Parameter]
		public bool Loading { get; set; }
	}
}
