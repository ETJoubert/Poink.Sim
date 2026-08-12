
using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Trader.Risk;

internal interface IRiskInspectable
{
    IReadOnlyList<RiskPolicyStatus> PolicyStatuses();
}
