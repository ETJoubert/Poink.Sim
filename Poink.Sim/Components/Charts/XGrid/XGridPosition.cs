using System;

namespace Poink.Sim.Components.Charts;

public enum XGridPositionDirection
{
	Long,
	Short,
}

// A trade overlaid on the XGrid, anchored to absolute price/time like XGridLine so it tracks
// its real position as the grid recomputes. A null CloseTime/ClosePrice marks a still-open
// trade: the renderer runs its line to the current price and shows a right-edge direction
// marker coloured by live profit/loss. StopLoss/TakeProfit draw as held levels while the
// trade is open. Public since it crosses the XGrid component boundary, same reason
// XGridResult and XGridLine are public rather than internal.
public sealed record XGridPosition(
	XGridPositionDirection Direction,
	DateTime OpenTime,
	double OpenPrice,
	DateTime? CloseTime = null,
	double? ClosePrice = null,
	double? StopLoss = null,
	double? TakeProfit = null
)
{
	public string? Id { get; init; }
}
