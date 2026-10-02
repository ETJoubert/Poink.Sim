using System.Collections.Generic;

namespace Poink.Sim.Components.Shared.Measurements;

/// <summary>One labelled, already-formatted value shown by the measurements panel.</summary>
public readonly record struct MeasurementItem(string Label, string Value, string Tooltip);

/// <summary>One selected thing's measurements under a header; every column lists the same labels in the same order.</summary>
public readonly record struct MeasurementColumn(string Header, IReadOnlyList<MeasurementItem> Items);
