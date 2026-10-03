using System.Collections.Generic;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Shared.IndicatorPanel;

/// <summary>A thin two-column panel listing indicators: the label in the first column, the value in the second.</summary>
public partial class IndicatorPanel : ComponentBase
{
	[Parameter, EditorRequired]
	public IReadOnlyList<IndicatorRow> Rows { get; set; } = [];
}
