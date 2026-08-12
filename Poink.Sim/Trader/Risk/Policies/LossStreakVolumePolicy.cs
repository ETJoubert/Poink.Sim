
using System;
using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Trader.Risk.Policies;

internal sealed class LossStreakVolumePolicy : IRiskPolicy
{
    public const string PolicyName = "Loss-streak throttle";
    public const int DefaultLossThreshold = 3;
    public const double DefaultVolumeFactor = 0.5;

    private readonly int _lossThreshold;
    private readonly double _volumeFactor;

    private int _processedTradeCount;
    private int _lossStreak;

    public LossStreakVolumePolicy(int lossThreshold = DefaultLossThreshold, double volumeFactor = DefaultVolumeFactor)
    {
        _lossThreshold = lossThreshold;
        _volumeFactor = volumeFactor;
    }

    public string Name => PolicyName;

    public string Description =>
        "After 3 consecutive losing trades, halves entry volume until a trade closes without a loss.";

    public string? Status =>
        _lossStreak == 0 ? null
        : _lossStreak >= _lossThreshold ? $"halving · {_lossStreak} losses"
        : $"{_lossStreak}/{_lossThreshold} losses";

    public IRiskPolicy Fresh() => new LossStreakVolumePolicy(_lossThreshold, _volumeFactor);

    public RiskReview Review(SimulationDecision proposed, RiskContext context)
    {
        if (proposed.Action is not (SimulationAction.EnterLong or SimulationAction.EnterShort))
            return RiskReview.Permit(proposed);
        if (_lossStreak < _lossThreshold)
            return RiskReview.Permit(proposed);

        return RiskReview.Modify(
            proposed with { CapitalFraction = proposed.CapitalFraction * _volumeFactor },
            $"volume cut to {_volumeFactor:P0} after {_lossStreak} consecutive losses");
    }

    // Trades close through the trader's own maintenance as well as manual closes, so newly-closed trades are folded into the streak here each tick rather than in Review.
    public IReadOnlyList<SimulationDecision> Supervise(RiskContext context)
    {
        for (int i = _processedTradeCount; i < context.Trades.Count; i++)
            _lossStreak = context.Trades[i].RealizedPnl < 0 ? _lossStreak + 1 : 0;
        _processedTradeCount = context.Trades.Count;
        return Array.Empty<SimulationDecision>();
    }
}
