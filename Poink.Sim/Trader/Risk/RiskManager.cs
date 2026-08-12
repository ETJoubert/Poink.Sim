
using System;
using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Trader.Risk;

internal sealed class RiskManager : IRiskManager
{
    private readonly IPositionManager _position;
    private readonly IReadOnlyList<IRiskPolicy> _policies;
    private readonly RiskPolicyToggles? _toggles;

    public RiskManager(IPositionManager position, IReadOnlyList<IRiskPolicy> policies, RiskPolicyToggles? toggles)
    {
        _position = position;
        _policies = policies;
        _toggles = toggles;
    }

    private bool IsActive(IRiskPolicy policy) => _toggles is null || _toggles.IsEnabled(policy.Name);

    // First deny is terminal; a modify feeds the changed decision into the next policy's review.
    public RiskReview Gate(SimulationDecision proposed, double bid, double ask, DateTime timestamp)
    {
        var context = _position.BuildContext(bid, ask, timestamp);
        var current = proposed;
        var modified = false;

        foreach (var policy in _policies)
        {
            if (!IsActive(policy))
                continue;

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

        foreach (var policy in _policies)
        {
            if (!IsActive(policy))
                continue;

            var injected = policy.Supervise(context);
            if (injected.Count > 0)
                (actions ??= new List<SimulationDecision>()).AddRange(injected);
        }

        return (IReadOnlyList<SimulationDecision>?)actions ?? Array.Empty<SimulationDecision>();
    }
}
