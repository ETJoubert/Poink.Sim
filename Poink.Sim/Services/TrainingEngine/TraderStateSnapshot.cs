using System.Collections.Generic;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// Raw trader/account values for one step, before TraderStateOptionsModel decides whether/how much
/// of it is actually included in the feature vector. All percentages are of cash balance, not a raw
/// money amount -- zero both when flat and when flat P/L. PreviousTradesRealized holds at most the
/// configured count of most recent closed trades, most recent first; FeatureAssembler pads it to that count.
/// </summary>
internal sealed record TraderStateSnapshot(double Unrealized, double Realized, IReadOnlyList<double> PreviousTradesRealized);
