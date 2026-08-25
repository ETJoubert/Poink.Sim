namespace Poink.Sim.Components.Charts;

/// <summary>
/// A resting order a host pins over the grid at its trigger price -- drawn as a dotted level with a
/// right-edge direction marker, distinct from live positions. Id makes it addressable so a click can
/// open it for modification or cancellation; Volume/StopLoss/TakeProfit seed the modify panel.
/// </summary>
public readonly record struct XGridPending(
	bool IsLong,
	double TriggerPrice,
	double Volume,
	double? StopLoss = null,
	double? TakeProfit = null
)
{
	public string? Id { get; init; }
}
