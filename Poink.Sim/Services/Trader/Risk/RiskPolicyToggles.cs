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

	public RiskPolicyToggles(
		IEnumerable<(string Name, string Description, IReadOnlyList<RiskPolicyParameter> Parameters)> policies
	)
	{
		var distinct = policies.GroupBy(p => p.Name).Select(g => g.First()).ToList();
		_order = distinct.Select(p => p.Name).ToList();
		_descriptions = distinct.ToDictionary(p => p.Name, p => p.Description);
		_enabled = _order.ToDictionary(name => name, _ => true);
		_parameters = distinct.ToDictionary(p => p.Name, p => p.Parameters.ToDictionary(par => par.Key, par => par));
	}

	public bool IsEnabled(string policyName)
	{
		lock (_lock)
		{
			return !_enabled.TryGetValue(policyName, out var enabled) || enabled;
		}
	}

	public bool Set(string policyName, bool enabled)
	{
		lock (_lock)
		{
			if (!_enabled.ContainsKey(policyName))
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

	// Carries a previous run's enabled flags, parameter values and run order over to a freshly built set;
	// policies the previous run didn't know keep their defaults and run after the carried-over ones.
	public void RestoreFrom(IReadOnlyList<RiskPolicySnapshot> previous)
	{
		lock (_lock)
		{
			var known = previous.Where(p => _enabled.ContainsKey(p.Name)).ToList();
			foreach (var state in known)
			{
				_enabled[state.Name] = state.Enabled;
				foreach (var parameter in state.Parameters)
					if (_parameters[state.Name].TryGetValue(parameter.Key, out var current))
						_parameters[state.Name][parameter.Key] = current with
						{
							Value = Math.Clamp(parameter.Value, current.Min, current.Max),
						};
			}

			var carriedOrder = known.Select(p => p.Name).ToList();
			var fullOrder = carriedOrder.Concat(_order.Except(carriedOrder)).ToList();
			_order.Clear();
			_order.AddRange(fullOrder);
		}
	}

	public IReadOnlyList<string> OrderedNames()
	{
		lock (_lock)
		{
			return _order.ToList();
		}
	}

	public IReadOnlyList<RiskPolicyParameter> ParametersFor(string policyName)
	{
		lock (_lock)
		{
			return _parameters.TryGetValue(policyName, out var parameters)
				? parameters.Values.ToList()
				: Array.Empty<RiskPolicyParameter>();
		}
	}

	public bool SetParameter(string policyName, string key, double value)
	{
		lock (_lock)
		{
			if (
				!_parameters.TryGetValue(policyName, out var parameters)
				|| !parameters.TryGetValue(key, out var current)
			)
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
				.Select(name => new RiskPolicySnapshot(
					name,
					_descriptions[name],
					_enabled[name],
					_parameters[name].Values.ToList()
				))
				.ToList();
		}
	}
}
