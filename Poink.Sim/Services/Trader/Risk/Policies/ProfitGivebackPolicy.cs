
using System;
using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Trader.Risk.Policies;

internal sealed class ProfitGivebackPolicy : IRiskPolicy
{
    public const string PolicyName = "Profit giveback stop";
    public const double DefaultGivebackPercent = 30;
    private const string GivebackKey = "Giveback";

    private double _givebackPercent;
    private double _peakUnrealizedPnl;

    public ProfitGivebackPolicy(double givebackPercent = DefaultGivebackPercent) => _givebackPercent = givebackPercent;

    public string Name => PolicyName;

    public string Description =>
        "Closes an open position once price gives back a set percentage of that position's peak unrealized gain.";

    public string? Status =>
        _peakUnrealizedPnl > 0 ? $"peak {_peakUnrealizedPnl:C2} · giveback {_givebackPercent:0}%" : null;

    // Expressed as a whole percentage (30 meaning 30%), matching how a person types and reads it, not the 0-1 fraction used internally.
    public IReadOnlyList<RiskPolicyParameter> Parameters =>
        [new RiskPolicyParameter(GivebackKey, "Giveback %", _givebackPercent, 5, 95, 5)];

    public void SetParameter(string key, double value)
    {
        if (key == GivebackKey)
            _givebackPercent = value;
    }

    public IRiskPolicy Fresh() => new ProfitGivebackPolicy(_givebackPercent);

    public RiskReview Review(SimulationDecision proposed, RiskContext context) => RiskReview.Permit(proposed);

    // Peak tracks this position's best unrealized gain since entry; a close resets it for the next position.
    public IReadOnlyList<SimulationDecision> Supervise(RiskContext context)
    {
        if (context.Position is not { } position)
        {
            _peakUnrealizedPnl = 0;
            return Array.Empty<SimulationDecision>();
        }

        double unrealized = position.UnrealizedPnl(context.Bid, context.Ask);
        _peakUnrealizedPnl = Math.Max(_peakUnrealizedPnl, unrealized);

        double givebackFraction = _givebackPercent / 100.0;
        if (_peakUnrealizedPnl <= 0 || unrealized > _peakUnrealizedPnl * (1 - givebackFraction))
            return Array.Empty<SimulationDecision>();

        _peakUnrealizedPnl = 0;
        return [new SimulationDecision(SimulationAction.Close, Reason: $"gave back {_givebackPercent:0}% of peak gain")];
    }
}
