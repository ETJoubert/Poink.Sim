namespace Poink.Sim.Components.Charts;

/// <summary>
/// One labelled reading shown in XGrid's footer readout rows and its cell tooltip.
/// Description feeds the footer's tooltip; Tone ("up"/"down") colours the footer value only.
/// </summary>
public readonly record struct XGridReadout(string Label, string Value, string Description, string? Tone = null);
