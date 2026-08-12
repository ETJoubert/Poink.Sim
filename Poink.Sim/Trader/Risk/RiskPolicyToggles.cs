
using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Trader.Risk;

internal sealed class RiskPolicyToggles
{
    private readonly object _lock = new();
    private readonly List<string> _order;
    private readonly Dictionary<string, bool> _enabled;

    public RiskPolicyToggles(IEnumerable<string> policyNames)
    {
        _order = policyNames.Distinct().ToList();
        _enabled = _order.ToDictionary(name => name, _ => true);
    }

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

    // delta -1 runs the policy earlier, +1 later; an out-of-range move is ignored.
    public void Move(string policyName, int delta)
    {
        lock (_lock)
        {
            int index = _order.IndexOf(policyName);
            if (index < 0)
                return;
            int target = index + delta;
            if (target < 0 || target >= _order.Count)
                return;
            (_order[index], _order[target]) = (_order[target], _order[index]);
        }
    }

    public IReadOnlyList<string> OrderedNames()
    {
        lock (_lock)
        { return _order.ToList(); }
    }

    public IReadOnlyList<RiskPolicyToggleState> States()
    {
        lock (_lock)
        { return _order.Select(name => new RiskPolicyToggleState(name, _enabled[name])).ToList(); }
    }
}

internal readonly record struct RiskPolicyToggleState(string Name, bool Enabled);
