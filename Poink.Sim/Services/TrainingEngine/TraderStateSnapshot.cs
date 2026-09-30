namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// Raw trader/account values for one step, before TraderStateOptionsModel decides whether it's
/// actually included in the feature vector. Unrealized is already expressed as a percent of cash
/// balance, not a raw money amount -- zero both when flat and when an open position's P/L is flat.
/// </summary>
internal sealed record TraderStateSnapshot(double Unrealized);
