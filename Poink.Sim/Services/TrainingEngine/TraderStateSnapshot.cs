using System.Collections.Generic;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// Raw trader/account values for one step, before TraderStateOptionsModel decides whether/how much
/// of it is actually included in the feature vector. All percentages are of cash balance, not a raw
/// money amount -- zero both when flat and when flat P/L. PreviousTradesRealized holds every closed
/// trade so far this episode, most recent first; FeatureAssembler trims/pads it to the configured count.
/// </summary>
internal sealed record TraderStateSnapshot(double Unrealized, double Realized, IReadOnlyList<double> PreviousTradesRealized);
