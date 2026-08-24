namespace Poink.Sim.Components.Charts;

// A resting order a host pins over the grid at its trigger price -- drawn as a dotted level
// with a right-edge direction marker in its own colour, distinct from live positions. Id
// makes it addressable so a click can open it for modification or cancellation; Volume/
// StopLoss/TakeProfit seed the modify panel. Host owned and recomputed per render. Public
// since it crosses the XGrid component boundary, same reason XGridPosition/XGridOrder are.
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
