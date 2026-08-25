using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Shared.StatisticsPanel;

/// <summary>
/// Renders TradeStatistics.Groups as labeled sections, instead of ViewDatatable's single flat
/// key/value table, with its own scoped styling since scoped CSS doesn't reach a child component.
/// </summary>
public partial class StatisticsPanel : ComponentBase
{
	[Parameter, EditorRequired]
	public IReadOnlyList<StatGroup> Groups { get; set; } = [];

	[Parameter]
	public string EmptyMessage { get; set; } = "No data to display.";

	private static string EmphasisClass(StatEmphasis emphasis) =>
		emphasis switch
		{
			StatEmphasis.Positive => "stp-value-positive",
			StatEmphasis.Negative => "stp-value-negative",
			_ => "",
		};
}
