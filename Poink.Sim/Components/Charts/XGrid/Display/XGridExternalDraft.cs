namespace Poink.Sim.Components.Charts;

/// <summary>
/// A not-yet-submitted order a host previews over the grid without going through XGrid's own
/// click-to-draft flow -- rendered the same way as a click-built draft (see XGrid.BuildPayload),
/// but only while the user isn't actively building one of their own.
/// </summary>
public readonly record struct XGridExternalDraft(double EntryPrice, bool IsLong, double? StopLoss, double? TakeProfit);
