namespace Poink.Sim.Components.Charts;

/// <summary>
/// A change to a resting order, raised when the user applies edits on the pending panel. The engine
/// cannot modify a resting order in place, so the host cancels PendingId and re-places it with these values.
/// </summary>
public readonly record struct XGridOrderModification(
	string PendingId,
	bool IsLong,
	double TriggerPrice,
	double Volume,
	double? StopLoss,
	double? TakeProfit
);
