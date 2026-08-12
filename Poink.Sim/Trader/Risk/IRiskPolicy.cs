
using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Trader.Risk;

internal interface IRiskPolicy
{
    string Name { get; }

    string Description { get; }

    // Human-readable live state for a stateful policy, or null when the policy carries no state worth surfacing.
    string? Status { get; }

    // Each trader receives its own instance for a run; a stateful policy returns a new object so per-position state never bleeds between traders.
    IRiskPolicy Fresh();

    RiskReview Review(SimulationDecision proposed, RiskContext context);

    IReadOnlyList<SimulationDecision> Supervise(RiskContext context);
}
