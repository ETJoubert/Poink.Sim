
using System;
using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Trader.Risk.Policies;

internal sealed class MaxOpenRiskPolicy : IRiskPolicy
{
    public const string PolicyName = "Max open risk";
    public const double DefaultMaxRiskFraction = 0.02;

    private readonly double _maxRiskFraction;

    public MaxOpenRiskPolicy(double maxRiskFraction = DefaultMaxRiskFraction) => _maxRiskFraction = maxRiskFraction;

    public string Name => PolicyName;

    public string Description =>
        "Rejects any entry without a stop-loss, and shrinks an entry whose stop-loss risk exceeds 2% of equity down to that cap.";

    public string? Status => null;

    public IRiskPolicy Fresh() => new MaxOpenRiskPolicy(_maxRiskFraction);

    public RiskReview Review(SimulationDecision proposed, RiskContext context)
    {
        if (proposed.Action is not (SimulationAction.EnterLong or SimulationAction.EnterShort))
            return RiskReview.Permit(proposed);

        var direction = proposed.Action == SimulationAction.EnterLong ? PositionDirection.Long : PositionDirection.Short;
        double riskCap = _maxRiskFraction * context.Capital.Equity;

        if (proposed.StopLossPrice is not { } stopLoss)
            return RiskReview.Deny(proposed, $"entry needs a stop-loss to bound risk to {_maxRiskFraction:P1} of equity", Name);

        double entryPrice = EntryPrice(proposed, direction, context);
        double projectedRisk = RiskHelper.ProjectedRiskOnStop(
            direction, context.Cash, proposed.CapitalFraction, context.Leverage, entryPrice, stopLoss);

        if (projectedRisk <= riskCap)
            return RiskReview.Permit(proposed);

        double cappedFraction = RiskHelper.CapitalFractionForRisk(riskCap, context.Cash, context.Leverage, entryPrice, stopLoss);
        if (cappedFraction <= 0)
            return RiskReview.Deny(proposed, $"cannot size entry within {_maxRiskFraction:P1} risk cap", Name);

        return RiskReview.Modify(
            proposed with { CapitalFraction = cappedFraction },
            $"capital fraction reduced to hold risk at {_maxRiskFraction:P1} of equity");
    }

    public IReadOnlyList<SimulationDecision> Supervise(RiskContext context) => Array.Empty<SimulationDecision>();

    private static double EntryPrice(SimulationDecision decision, PositionDirection direction, RiskContext context) =>
        decision.TriggerPrice ?? (direction == PositionDirection.Long ? context.Ask : context.Bid);
}
