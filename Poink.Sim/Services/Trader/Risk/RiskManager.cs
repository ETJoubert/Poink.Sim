using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;

namespace Poink.Sim.Trader.Risk;

internal sealed class RiskManager : IRiskManager
{
	private readonly IPositionManager _position;
	private readonly IReadOnlyList<IRiskPolicy> _policies;
	private readonly IReadOnlyDictionary<string, IRiskPolicy> _byName;
	private readonly RiskPolicyToggles? _toggles;

	public RiskManager(IPositionManager position, IReadOnlyList<IRiskPolicy> policies, RiskPolicyToggles? toggles)
	{
		_position = position;
		_policies = policies;
		_byName = policies.ToDictionary(policy => policy.Name);
		_toggles = toggles;
	}

	// With toggles present, execution order, enablement and parameter values are the run's live shared state; without them, the creation order runs in full at each policy's own defaults.
	private IEnumerable<IRiskPolicy> ActivePolicies()
	{
		var active = _toggles is null
			? _policies
			: _toggles
				.OrderedNames()
				.Where(name => _toggles.IsEnabled(name) && _byName.ContainsKey(name))
				.Select(name => _byName[name]);

		foreach (var policy in active)
		{
			SyncParameters(policy);
			yield return policy;
		}
	}

	private void SyncParameters(IRiskPolicy policy)
	{
		if (_toggles is null)
			return;
		foreach (var parameter in _toggles.ParametersFor(policy.Name))
			policy.SetParameter(parameter.Key, parameter.Value);
	}

	// First deny is terminal; a modify feeds the changed decision into the next policy's review.
	public RiskReview Gate(SimulationDecision proposed, double bid, double ask, DateTime timestamp)
	{
		var context = _position.BuildContext(bid, ask, timestamp);
		var current = proposed;
		var modified = false;

		foreach (var policy in ActivePolicies())
		{
			var review = policy.Review(current, context);
			if (review.Verdict == RiskVerdict.Deny)
				return review;
			if (review.Verdict == RiskVerdict.Modify)
			{
				current = review.Decision;
				modified = true;
			}
		}

		return modified ? RiskReview.Modify(current, "adjusted by risk policy") : RiskReview.Permit(current);
	}

	public IReadOnlyList<SimulationDecision> Supervise(double bid, double ask, DateTime timestamp)
	{
		var context = _position.BuildContext(bid, ask, timestamp);
		List<SimulationDecision>? actions = null;

		foreach (var policy in ActivePolicies())
		{
			var injected = policy.Supervise(context);
			if (injected.Count > 0)
				(actions ??= new List<SimulationDecision>()).AddRange(injected);
		}

		return (IReadOnlyList<SimulationDecision>?)actions ?? Array.Empty<SimulationDecision>();
	}

	public IReadOnlyList<RiskPolicyStatus> PolicyStatuses() =>
		_policies
			.Where(policy => policy.Status is not null)
			.Select(policy => new RiskPolicyStatus(policy.Name, policy.Status!))
			.ToList();

	public IReadOnlyList<RiskPolicySnapshot> PolicyStates() => _toggles?.States() ?? Array.Empty<RiskPolicySnapshot>();

	public bool PoliciesLocked => _toggles?.Locked ?? false;

	public bool SetPolicyEnabled(string policyName, bool enabled) =>
		_toggles is not null && _toggles.Set(policyName, enabled);

	public bool MovePolicy(string policyName, int delta) => _toggles is not null && _toggles.Move(policyName, delta);

	public bool SetPolicyParameter(string policyName, string key, double value) =>
		_toggles is not null && _toggles.SetParameter(policyName, key, value);
}
