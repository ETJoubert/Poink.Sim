using System.Text.Json.Serialization;

namespace Poink.Sim.Components.Charts;

/// <summary>
/// One label/value line in the cell hover tooltip -- returned as a list so xGrid.js can render
/// each as its own row instead of one long joined string. Property names are pinned explicitly
/// since this crosses the JS interop boundary.
/// </summary>
public readonly record struct XGridTooltipRow(
	[property: JsonPropertyName("label")] string Label,
	[property: JsonPropertyName("value")] string Value
);
