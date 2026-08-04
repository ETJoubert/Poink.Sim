
using System;

namespace Poink.Sim.Training;

internal sealed class TradingRulesRewardPolicy : IRewardPolicy
{
    private readonly double _lossAversionMultiplier;
    private readonly double _entryCostFraction;
    private readonly TimeSpan _maxHoldingDuration;
    private readonly double _overholdPenaltyFractionPerHour;
    private readonly double _realizedBonusFraction;

    public TradingRulesRewardPolicy(
        double lossAversionMultiplier = 1.5,
        double entryCostFraction = 0.0005,
        TimeSpan? maxHoldingDuration = null,
        double overholdPenaltyFractionPerHour = 0.0002,
        double realizedBonusFraction = 0.1)
    {
        _lossAversionMultiplier = lossAversionMultiplier;
        _entryCostFraction = entryCostFraction;
        _maxHoldingDuration = maxHoldingDuration ?? TimeSpan.FromHours(1);
        _overholdPenaltyFractionPerHour = overholdPenaltyFractionPerHour;
        _realizedBonusFraction = realizedBonusFraction;
    }

    public string Name => "Trading Rules (loss-averse, anti-churn, max holding time, realize-over-unrealize)";

    public double ComputeReward(RewardContext context)
    {
        double reward = context.CapitalNow.Equity - context.PreviousCapital.Equity;
        if (reward < 0)
            reward *= _lossAversionMultiplier;

        bool justOpened = context.PreviousPosition is null && context.Position is not null;
        if (justOpened)
            reward -= _entryCostFraction * context.CapitalNow.Equity;

        if (context.Position is { } position)
        {
            var held = context.TimestampUtc - position.EntryTime;
            if (held > _maxHoldingDuration)
                reward -= _overholdPenaltyFractionPerHour * context.CapitalNow.Equity * (held - _maxHoldingDuration).TotalHours;
        }

        double realizedDelta = context.CapitalNow.RealizedPnl - context.PreviousCapital.RealizedPnl;
        if (realizedDelta > 0)
            reward += _realizedBonusFraction * realizedDelta;

        return reward;
    }
}
