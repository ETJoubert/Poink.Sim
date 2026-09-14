using System.Text.Json.Serialization;

namespace Poink.Sim.Components.Charts;

/// <summary>
/// One label/value line in a hover tooltip (cell or line) -- returned as a list so xGrid.js can
/// render each as its own row instead of one long joined string. Property names are pinned
/// explicitly since this crosses the JS interop boundary. Tone optionally colors the row
/// (e.g. "gain"/"loss") -- cell rows never set it.
/// </summary>
public readonly record struct XGridTooltipRow(
	[property: JsonPropertyName("label")] string Label,
	[property: JsonPropertyName("value")] string Value,
	[property: JsonPropertyName("tone")] string? Tone = null
);
