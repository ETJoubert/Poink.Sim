
using System;
using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Trader.Risk;

internal sealed class RiskPolicyToggles
{
    private readonly object _lock = new();
    private readonly List<string> _order;
    private readonly Dictionary<string, string> _descriptions;
    private readonly Dictionary<string, bool> _enabled;
    private readonly Dictionary<string, Dictionary<string, RiskPolicyParameter>> _parameters;

    public bool Locked { get; }

    public RiskPolicyToggles(IEnumerable<(string Name, string Description, IReadOnlyList<RiskPolicyParameter> Parameters)> policies, bool locked)
    {
        var distinct = policies.GroupBy(p => p.Name).Select(g => g.First()).ToList();
        _order = distinct.Select(p => p.Name).ToList();
        _descriptions = distinct.ToDictionary(p => p.Name, p => p.Description);
        _enabled = _order.ToDictionary(name => name, _ => true);
        _parameters = distinct.ToDictionary(p => p.Name, p => p.Parameters.ToDictionary(par => par.Key, par => par));
        Locked = locked;
    }

    public bool IsEnabled(string policyName)
    {
        lock (_lock)
        { return !_enabled.TryGetValue(policyName, out var enabled) || enabled; }
    }

    public bool Set(string policyName, bool enabled)
    {
        lock (_lock)
        {
            if (Locked || !_enabled.ContainsKey(policyName))
                return false;
            _enabled[policyName] = enabled;
            return true;
        }
    }

    // delta -1 runs the policy earlier, +1 later; an out-of-range move is ignored.
    public bool Move(string policyName, int delta)
    {
        lock (_lock)
        {
            if (Locked)
                return false;
            int index = _order.IndexOf(policyName);
            if (index < 0)
                return false;
            int target = index + delta;
            if (target < 0 || target >= _order.Count)
                return false;
            (_order[index], _order[target]) = (_order[target], _order[index]);
            return true;
        }
    }

    public IReadOnlyList<string> OrderedNames()
    {
        lock (_lock)
        { return _order.ToList(); }
    }

    public IReadOnlyList<RiskPolicyParameter> ParametersFor(string policyName)
    {
        lock (_lock)
        { return _parameters.TryGetValue(policyName, out var parameters) ? parameters.Values.ToList() : Array.Empty<RiskPolicyParameter>(); }
    }

    public bool SetParameter(string policyName, string key, double value)
    {
        lock (_lock)
        {
            if (Locked || !_parameters.TryGetValue(policyName, out var parameters) || !parameters.TryGetValue(key, out var current))
                return false;
            parameters[key] = current with { Value = Math.Clamp(value, current.Min, current.Max) };
            return true;
        }
    }

    public IReadOnlyList<RiskPolicySnapshot> States()
    {
        lock (_lock)
        {
            return _order
                .Select(name => new RiskPolicySnapshot(name, _descriptions[name], _enabled[name], _parameters[name].Values.ToList()))
                .ToList();
        }
    }
}
