
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Poink.Sim.Trader.Risk;

/// <summary>
/// Catalog of every <see cref="IRiskPolicy"/> in this assembly, and the seam that wraps a core
/// trader in the risk decorator for a run's selected policies.
/// </summary>
internal sealed class RiskPolicyService : IRiskPolicyService
{
    private readonly IReadOnlyDictionary<string, IRiskPolicy> _prototypes;

    public RiskPolicyService(IServiceProvider provider)
    {
        var policyTypes = typeof(IRiskPolicy).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                && typeof(IRiskPolicy).IsAssignableFrom(type));

        _prototypes = policyTypes
            .Select(type => (IRiskPolicy)ActivatorUtilities.CreateInstance(provider, type))
            .ToDictionary(policy => policy.Name);
    }

    public IReadOnlyList<RiskPolicyInfo> AvailablePolicies =>
        _prototypes.Values.Select(policy => new RiskPolicyInfo(policy.Name, policy.Description)).ToList();

    // No active policy leaves the core trader untouched, keeping the layer additive.
    public ITrader Wrap(ITrader core, IReadOnlyList<string>? policyNames, RiskPolicyToggles? toggles = null)
    {
        var policies = CreateActive(policyNames);
        if (policies.Count == 0)
            return core;

        var positionManager = new PositionManager(core);
        var riskManager = new RiskManager(positionManager, policies, toggles);
        return new RiskAwareTrader(core, riskManager);
    }

    public RiskPolicyToggles? CreateToggles(IReadOnlyList<string>? policyNames, bool locked)
    {
        if (policyNames is not { Count: > 0 })
            return null;

        var entries = policyNames
            .Where(_prototypes.ContainsKey)
            .Select(name => (name, _prototypes[name].Description, _prototypes[name].Parameters));
        return new RiskPolicyToggles(entries, locked);
    }

    private IReadOnlyList<IRiskPolicy> CreateActive(IReadOnlyList<string>? policyNames)
    {
        if (policyNames is null || policyNames.Count == 0)
            return Array.Empty<IRiskPolicy>();

        return policyNames
            .Where(_prototypes.ContainsKey)
            .Select(name => _prototypes[name].Fresh())
            .ToList();
    }
}
