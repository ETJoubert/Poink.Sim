using System.Collections.Generic;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Charts
{
	/// <summary>One labelled footer line of XGrid readings in equal-width slots, so values never shift their neighbours.</summary>
	public partial class XGridReadoutRow : ComponentBase
	{
		[Parameter, EditorRequired]
		public string Label { get; set; } = "";

		[Parameter]
		public string LabelTitle { get; set; } = "";

		/// <summary>Null shows a single "--" placeholder titled with EmptyTitle.</summary>
		[Parameter]
		public IReadOnlyList<XGridReadout>? Readouts { get; set; }

		[Parameter]
		public string EmptyTitle { get; set; } = "";
	}
}
