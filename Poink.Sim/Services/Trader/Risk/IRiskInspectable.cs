
using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Trader.Risk;

internal interface IRiskInspectable
{
    IReadOnlyList<RiskPolicyStatus> PolicyStatuses();

    IReadOnlyList<RiskPolicySnapshot> PolicyStates();

    bool PoliciesLocked { get; }

    bool SetPolicyEnabled(string policyName, bool enabled);

    bool MovePolicy(string policyName, int delta);

    bool SetPolicyParameter(string policyName, string key, double value);

    RiskPolicyDenial? LastDenial { get; }
}
