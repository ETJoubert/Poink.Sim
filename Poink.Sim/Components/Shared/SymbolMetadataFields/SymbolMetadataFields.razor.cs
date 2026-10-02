using Microsoft.AspNetCore.Components;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Shared.SymbolMetadataFields
{
	/// <summary>Read-only, labelled trading parameters of a symbol (digits, pip size, volume limits, swaps) for a host's setup panel.</summary>
	public partial class SymbolMetadataFields : ComponentBase
	{
		[Parameter]
		public SymbolModel? Symbol { get; set; }

		[Parameter]
		public SymbolMetadataModel? Metadata { get; set; }

		[Parameter]
		public bool Loading { get; set; }
	}
}
