
using System;
using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Trader.Risk.Policies;

internal sealed class PercentStopLossPolicy : IRiskPolicy
{
    public const string PolicyName = "2% stop-loss";
    public const double DefaultStopFraction = 0.02;

    private readonly double _stopFraction;

    public PercentStopLossPolicy(double stopFraction = DefaultStopFraction) => _stopFraction = stopFraction;

    public string Name => PolicyName;

    public string Description =>
        "Attaches a 2% stop-loss to any entry that has none -- 2% below entry for a long, above for a short. Leaves an entry's existing stop-loss alone.";

    public IRiskPolicy Fresh() => new PercentStopLossPolicy(_stopFraction);

    public RiskReview Review(SimulationDecision proposed, RiskContext context)
    {
        if (proposed.Action is not (SimulationAction.EnterLong or SimulationAction.EnterShort))
            return RiskReview.Permit(proposed);
        if (proposed.StopLossPrice is not null)
            return RiskReview.Permit(proposed);

        var direction = proposed.Action == SimulationAction.EnterLong ? PositionDirection.Long : PositionDirection.Short;
        double entryPrice = proposed.TriggerPrice ?? (direction == PositionDirection.Long ? context.Ask : context.Bid);
        if (entryPrice <= 0)
            return RiskReview.Permit(proposed);

        double stopPrice = entryPrice * (1 + (direction == PositionDirection.Long ? -_stopFraction : _stopFraction));
        return RiskReview.Modify(
            proposed with { StopLossPrice = stopPrice },
            $"attached a {_stopFraction:P1} stop-loss");
    }

    public IReadOnlyList<SimulationDecision> Supervise(RiskContext context) => Array.Empty<SimulationDecision>();
}
