using System;

namespace Poink.Sim.Components.Charts;

public enum XGridLineKind
{
	Segment,
	Vertical,
	Horizontal,
}

// A line overlaid on the XGrid, anchored to absolute price/time rather than grid cell
// indices so it tracks its real position as the grid recomputes -- the renderer re-maps the
// anchors onto whatever rows/columns the current XGridResult exposes. Segment joins two
// price/time points; Vertical pins to a time and spans every price; Horizontal pins to a
// price and spans every time. Public since it crosses the XGrid component boundary, same
// reason XGridResult is public rather than internal.
public sealed record XGridLine(
	XGridLineKind Kind,
	DateTime StartTime,
	double StartPrice,
	DateTime EndTime,
	double EndPrice
)
{
	public string? Id { get; init; }

	public static XGridLine Segment(DateTime startTime, double startPrice, DateTime endTime, double endPrice) =>
		new(XGridLineKind.Segment, startTime, startPrice, endTime, endPrice);

	public static XGridLine Vertical(DateTime time) => new(XGridLineKind.Vertical, time, 0, time, 0);

	public static XGridLine Horizontal(double price) => new(XGridLineKind.Horizontal, default, price, default, price);
}
