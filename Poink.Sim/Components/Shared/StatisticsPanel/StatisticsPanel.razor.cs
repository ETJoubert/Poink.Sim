using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Shared.StatisticsPanel;

// Renders TradeStatistics.Groups as labeled sections (see the model class for
// how the categories are built) instead of ViewDatatable's single flat
// key/value table -- own scoped styling, since a parent's scoped CSS classes
// don't reach into a child component's DOM (same reason DataTable exposes
// its own data-table-value-positive/negative, see TraderCellClass).
public partial class StatisticsPanel : ComponentBase
{
    [Parameter, EditorRequired]
    public IReadOnlyList<StatGroup> Groups { get; set; } = [];

    [Parameter]
    public string EmptyMessage { get; set; } = "No data to display.";

    private static string EmphasisClass(StatEmphasis emphasis) => emphasis switch
    {
        StatEmphasis.Positive => "stp-value-positive",
        StatEmphasis.Negative => "stp-value-negative",
        _ => "",
    };
}
