using System;
using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Trader.Risk;

internal interface IRiskPolicy
{
	string Name { get; }

	string Description { get; }

	// Human-readable live state for a stateful policy, or null when the policy carries no state worth surfacing.
	string? Status { get; }

	// Adjustable knobs this policy exposes to the run's toggles/UI; empty for a policy with no tunable values.
	IReadOnlyList<RiskPolicyParameter> Parameters => Array.Empty<RiskPolicyParameter>();

	// Applies a live value for one of this policy's Parameters keys; a policy with no parameters leaves this a no-op.
	void SetParameter(string key, double value) { }

	// Each trader receives its own instance for a run; a stateful policy returns a new object so per-position state never bleeds between traders.
	IRiskPolicy Fresh();

	RiskReview Review(SimulationDecision proposed, RiskContext context);

	IReadOnlyList<SimulationDecision> Supervise(RiskContext context);
}
