
using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Trader.Risk;

internal sealed class RiskPolicyToggles
{
    private readonly object _lock = new();
    private readonly Dictionary<string, bool> _enabled;

    public RiskPolicyToggles(IEnumerable<string> policyNames) =>
        _enabled = policyNames.Distinct().ToDictionary(name => name, _ => true);

    public bool IsEnabled(string policyName)
    {
        lock (_lock)
        { return !_enabled.TryGetValue(policyName, out var enabled) || enabled; }
    }

    public void Set(string policyName, bool enabled)
    {
        lock (_lock)
        {
            if (_enabled.ContainsKey(policyName))
                _enabled[policyName] = enabled;
        }
    }

    public IReadOnlyList<RiskPolicyToggleState> States()
    {
        lock (_lock)
        { return _enabled.Select(pair => new RiskPolicyToggleState(pair.Key, pair.Value)).ToList(); }
    }
}

internal readonly record struct RiskPolicyToggleState(string Name, bool Enabled);
