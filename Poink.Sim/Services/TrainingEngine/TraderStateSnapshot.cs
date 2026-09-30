namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// Raw trader/account values for one step, before TraderStateOptionsModel decides whether it's
/// actually included in the feature vector. Both are already expressed as a percent of cash
/// balance, not a raw money amount -- zero both when flat and when flat P/L.
/// </summary>
internal sealed record TraderStateSnapshot(double Unrealized, double Realized);
