
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
