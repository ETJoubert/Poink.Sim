
using System.Collections.Generic;

namespace Poink.Sim.Trader.Risk;

internal interface IRiskPolicyRegistry
{
    IReadOnlyList<RiskPolicyInfo> AvailablePolicies { get; }

    IReadOnlyList<IRiskPolicy> CreateActive(IReadOnlyList<string>? policyNames);
}
