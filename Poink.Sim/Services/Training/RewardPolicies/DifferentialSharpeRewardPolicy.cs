// ── Reward policy: differential Sharpe ratio (risk-adjusted return) ─────
//  EquityDriftRewardPolicy and TradingRulesRewardPolicy both reward raw
//  equity movement (optionally shaped) -- a trader who cares about risk-
//  adjusted return wants the reward itself to distinguish "up 1% smoothly"
//  from "up 1% via a coin flip," which raw drift cannot do since both look
//  identical to it. This policy instead rewards the step-by-step change in
//  the trailing Sharpe ratio, using the Differential Sharpe Ratio (DSR) from
//  Moody & Saffell, "Reinforcement Learning for Trading" (NeurIPS 1998):
//  the exact derivative of the Sharpe ratio with respect to the exponential
//  weighting rate, evaluated at the current step. Summing DSR across an
//  episode telescopes to (approximately) the total change in Sharpe ratio
//  over that episode -- a differentiable, O(1)-per-step stand-in for what
//  would otherwise be a single sparse reward computed only once, at the very
//  end of the episode, from the whole equity curve. A sparse end-of-episode
//  reward would still work in principle (PolicyGradientTrainingStrategy's
//  discounted reward-to-go credits every step in the trajectory that earned
//  it either way) but wastes most of the per-step training signal this
//  codebase's REINFORCE setup is built to consume.
//
//  Let r_t be this step's return (equity drift as a fraction of the previous
//  step's equity, not a raw dollar amount -- Sharpe ratio is scale-free by
//  construction, so dollar drift alone would make the ratio meaningless).
//  A_t/B_t are exponential moving estimates of r_t's first and second moment
//  (mean and mean-square), updated with adaptation rate Eta:
//
//   DeltaA_t = r_t - A_{t-1}
//   DeltaB_t = r_t^2 - B_{t-1}
//   D_t = (B_{t-1} * DeltaA_t - 0.5 * A_{t-1} * DeltaB_t) / (B_{t-1} - A_{t-1}^2)^1.5
//   A_t = A_{t-1} + Eta * DeltaA_t
//   B_t = B_{t-1} + Eta * DeltaB_t
//
//  D_t is the reward returned for this step. The denominator is the
//  trailing variance estimate to the 3/2 power; it's ~0 for the first few
//  steps of an episode (A_0 = B_0 = 0), so D_t is reported as 0 until the
//  variance estimate clears VarianceFloor rather than divide-by-near-zero
//  into a huge, meaningless spike.
//
//  A_t/B_t are per-instance mutable state, unlike EquityDriftRewardPolicy/
//  TradingRulesRewardPolicy's pure ComputeReward -- IRewardPolicy instances
//  are long-lived DI singletons (see Program.cs) reused across every
//  episode, so without a reset this policy's variance estimate would drift
//  across episode (and training run) boundaries instead of describing only
//  the episode currently being scored. OnEpisodeStart (called once per
//  episode by RlEpisodeRunner.Run) is exactly that reset.
// ─────────────────────────────────────────────────────────────────────────
using System;

namespace Poink.Sim.Training;

internal sealed class DifferentialSharpeRewardPolicy : IRewardPolicy
{
    private const double VarianceFloor = 1e-8;

    private readonly double _eta;
    private double _a;
    private double _b;

    public DifferentialSharpeRewardPolicy(double eta = 0.01)
    {
        _eta = eta;
    }

    public string Name => "Differential Sharpe Ratio (risk-adjusted)";

    public void OnEpisodeStart()
    {
        _a = 0;
        _b = 0;
    }

    public double ComputeReward(RewardContext context)
    {
        double previousEquity = context.PreviousCapital.Equity;
        double r = previousEquity != 0
            ? (context.CapitalNow.Equity - previousEquity) / previousEquity
            : 0;

        double deltaA = r - _a;
        double deltaB = r * r - _b;
        double variance = _b - _a * _a;

        double reward = variance > VarianceFloor
            ? (_b * deltaA - 0.5 * _a * deltaB) / Math.Pow(variance, 1.5)
            : 0;

        _a += _eta * deltaA;
        _b += _eta * deltaB;

        return reward;
    }
}
