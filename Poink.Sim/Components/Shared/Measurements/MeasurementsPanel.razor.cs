using System.Collections.Generic;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Shared.Measurements;

/// <summary>Lists labelled measurements as a table with one row per selection; the host decides what is selected and what is measured.</summary>
public partial class MeasurementsPanel : ComponentBase
{
	[Parameter]
	public string Title { get; set; } = "MEASUREMENTS";

	[Parameter]
	public IReadOnlyList<MeasurementColumn> Columns { get; set; } = [];

	/// <summary>Raised with the column index when the user removes that column's selection.</summary>
	[Parameter]
	public EventCallback<int> OnRemove { get; set; }

	[Parameter]
	public string EmptyMessage { get; set; } = "nothing selected";
}
