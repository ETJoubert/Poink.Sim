namespace Poink.Sim.Components.Charts;

// An order drafted on the XGrid's right-margin panel and handed to a host via OnOrderPlaced.
// Prices are absolute so the host maps them onto its own order model; StopLoss/TakeProfit are
// null until the user pins them. Public since it crosses the XGrid component boundary, same
// reason XGridLine and XGridPosition are public rather than internal.
public readonly record struct XGridOrder(
    bool IsLong, double EntryPrice, double Volume, double? StopLoss, double? TakeProfit);
