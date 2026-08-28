namespace Poink.Sim.Components.Charts;

/// <summary>
/// An order drafted on the XGrid's right-margin panel and handed to a host via OnOrderPlaced. Prices are
/// absolute so the host maps them onto its own order model; StopLoss/TakeProfit are null until the user pins them.
/// </summary>
public readonly record struct XGridOrder(
	bool IsLong,
	double EntryPrice,
	double Volume,
	double? StopLoss,
	double? TakeProfit
);
