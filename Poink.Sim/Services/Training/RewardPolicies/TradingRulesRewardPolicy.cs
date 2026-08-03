// ── Reward policy: equity drift shaped by four trading rules ────────────
//  Same equity-drift base as EquityDriftRewardPolicy, with four additive/
//  multiplicative shaping terms layered on top -- each translates one
//  informal trading rule into a differentiable reward term, so
//  PolicyGradientTrainingStrategy's discounting/mean-centered baseline keeps
//  working unchanged on whatever this produces per step:
//
//   - "Do not lose money": a negative step reward is scaled by
//     LossAversionMultiplier (>1) -- losses are felt more strongly than an
//     equal-sized gain is rewarded (a hard "never lose" constraint isn't
//     expressible as a differentiable reward, so this is the standard
//     loss-aversion translation instead).
//   - "Don't need to trade every tick": opening a new position (a flat ->
//     held transition since the previous decision) costs EntryCostFraction
//     of current equity -- discourages churning through entries for their
//     own sake without forbidding trading outright. Holding/Hold already
//     costs nothing, so there's no separate "reward for inaction" term
//     needed on top of this.
//   - "A position should not stay open too long": once the current
//     position's age (TimestampUtc - Position.EntryTime) exceeds
//     MaxHoldingDuration, a penalty (OverholdPenaltyFractionPerHour of
//     equity, per hour past the threshold) accrues every step it stays
//     open past that point -- a soft, growing pressure to close rather
//     than a hard cutoff.
//   - "Realized is better than unrealized": whenever RealizedPnl increases
//     since the previous decision (a profitable Close/partial-realization
//     happened), that realized gain is topped up by an extra
//     RealizedBonusFraction of itself -- the base equity-drift term already
//     counts the dollar amount once regardless of whether it's realized or
//     still unrealized (Equity = Cash + UnrealizedPnl either way), so this
//     is a pure nudge toward locking in gains rather than letting them ride
//     at risk, not a re-count of the same money. Only fires on a realized
//     *gain* -- a realized loss is already penalized by loss-aversion above.
//
//  All constants are starting points, not tuned values -- in particular,
//  size MaxHoldingDuration relative to whatever episodeBars *
//  barWidthSeconds a given training run actually uses.
// ─────────────────────────────────────────────────────────────────────────
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
        if (reward < 0) reward *= _lossAversionMultiplier;

        bool justOpened = context.PreviousPosition is null && context.Position is not null;
        if (justOpened) reward -= _entryCostFraction * context.CapitalNow.Equity;

        if (context.Position is { } position)
        {
            var held = context.TimestampUtc - position.EntryTime;
            if (held > _maxHoldingDuration)
                reward -= _overholdPenaltyFractionPerHour * context.CapitalNow.Equity * (held - _maxHoldingDuration).TotalHours;
        }

        double realizedDelta = context.CapitalNow.RealizedPnl - context.PreviousCapital.RealizedPnl;
        if (realizedDelta > 0) reward += _realizedBonusFraction * realizedDelta;

        return reward;
    }
}
