
using System;
using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Trader.Risk;

internal sealed class RiskPolicyRegistry : IRiskPolicyRegistry
{
    private readonly IReadOnlyDictionary<string, IRiskPolicy> _prototypes;

    public RiskPolicyRegistry(IEnumerable<IRiskPolicy> policies) =>
        _prototypes = policies.ToDictionary(policy => policy.Name);

    public IReadOnlyList<RiskPolicyInfo> AvailablePolicies =>
        _prototypes.Values.Select(policy => new RiskPolicyInfo(policy.Name, policy.Description)).ToList();

    public IReadOnlyList<IRiskPolicy> CreateActive(IReadOnlyList<string>? policyNames)
    {
        if (policyNames is null || policyNames.Count == 0)
            return Array.Empty<IRiskPolicy>();

        return policyNames
            .Where(_prototypes.ContainsKey)
            .Select(name => _prototypes[name].Fresh())
            .ToList();
    }
}
