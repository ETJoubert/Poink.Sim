namespace Poink.Sim.Components.Charts;

/// <summary>
/// One labelled reading of a single XGrid cell, shared by the hover tooltip and the footer readout.
/// Description feeds the footer's tooltip; Tone ("up"/"down") colours the footer value only.
/// </summary>
public readonly record struct XGridCellMeasurement(string Label, string Value, string Description, string? Tone = null);
