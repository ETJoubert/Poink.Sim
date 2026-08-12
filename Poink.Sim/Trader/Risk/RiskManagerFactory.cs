
using System.Collections.Generic;

namespace Poink.Sim.Trader.Risk;

internal sealed class RiskManagerFactory : IRiskManagerFactory
{
    private readonly IRiskPolicyRegistry _registry;

    public RiskManagerFactory(IRiskPolicyRegistry registry) => _registry = registry;

    // No active policy leaves the core trader untouched, keeping the layer additive.
    public ITrader Wrap(ITrader core, IReadOnlyList<string>? policyNames, RiskPolicyToggles? toggles = null)
    {
        var policies = _registry.CreateActive(policyNames);
        if (policies.Count == 0)
            return core;

        var positionManager = new PositionManager(core);
        var riskManager = new RiskManager(positionManager, policies, toggles);
        return new RiskAwareTrader(core, riskManager);
    }
}
