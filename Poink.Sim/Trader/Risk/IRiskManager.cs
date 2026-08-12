
using System;
using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Trader.Risk;

internal interface IRiskManager
{
    RiskReview Gate(SimulationDecision proposed, double bid, double ask, DateTime timestamp);

    IReadOnlyList<SimulationDecision> Supervise(double bid, double ask, DateTime timestamp);

    IReadOnlyList<RiskPolicyStatus> PolicyStatuses();
}
