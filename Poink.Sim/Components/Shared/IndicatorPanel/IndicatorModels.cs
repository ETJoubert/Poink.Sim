namespace Poink.Sim.Components.Shared.IndicatorPanel;

/// <summary>One labelled, already-formatted indicator value shown by the indicator panel.</summary>
public readonly record struct IndicatorRow(string Label, string Value, string Tooltip);
