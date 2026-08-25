using System;
using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Trader.Risk;

internal interface IRiskPolicy
{
	string Name { get; }

	string Description { get; }

	/// <summary>
	/// Human-readable live state for a stateful policy, or null when the policy carries no state worth surfacing.
	/// </summary>
	string? Status { get; }

	/// <summary>
	/// Adjustable knobs this policy exposes to the run's toggles/UI; empty for a policy with no tunable values.
	/// </summary>
	IReadOnlyList<RiskPolicyParameter> Parameters => Array.Empty<RiskPolicyParameter>();

	/// <summary>
	/// Applies a live value for one of this policy's <see cref="Parameters"/> keys; a policy with no parameters leaves this a no-op.
	/// </summary>
	void SetParameter(string key, double value) { }

	/// <summary>
	/// Each trader receives its own instance for a run; a stateful policy returns a new object so per-position state never bleeds between traders.
	/// </summary>
	IRiskPolicy Fresh();

	RiskReview Review(SimulationDecision proposed, RiskContext context);

	IReadOnlyList<SimulationDecision> Supervise(RiskContext context);
}
