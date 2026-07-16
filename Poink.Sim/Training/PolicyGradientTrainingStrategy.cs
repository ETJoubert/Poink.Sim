// ── v1 concrete strategy: REINFORCE with a mean-centered baseline ────────
//  Discounted reward-to-go per step (gamma = 0.99), mean-centered and
//  std-normalized across the episode as a simple variance-reduction
//  baseline (no separate value network needed for a first setup). Loss is
//  -mean(logProb_t * normalizedReturn_t): minimizing this is gradient ascent
//  on expected return, the standard REINFORCE update.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Linq;
using Poink.Sim.Brain;
using Poink.Sim.Brain.Neural;
using TorchSharp;
using static TorchSharp.torch;

namespace Poink.Sim.Training;

internal sealed class PolicyGradientTrainingStrategy : IRlTrainingStrategy
{
    private const double DiscountFactor = 0.99;

    public string Name => "Policy Gradient (REINFORCE)";

    public bool CanTrain(ISimulationBrain brain) => brain is IPolicyGradientBrain;

    public optim.Optimizer CreateOptimizer(ISimulationBrain brain, double learningRate)
    {
        if (brain is not IPolicyGradientBrain policyBrain)
            throw new InvalidOperationException($"{Name} cannot train brain '{brain.Name}'");
        return optim.Adam(policyBrain.Network.parameters(), lr: learningRate);
    }

    public double TrainEpisode(ISimulationBrain brain, optim.Optimizer optimizer, RlEpisode episode)
    {
        if (brain is not IPolicyGradientBrain)
            throw new InvalidOperationException($"{Name} cannot train brain '{brain.Name}'");
        if (episode.Steps.Count == 0)
            return 0;

        using var scope = NewDisposeScope();

        var returns = new double[episode.Steps.Count];
        double runningReturn = 0;
        for (int i = episode.Steps.Count - 1; i >= 0; i--)
        {
            runningReturn = episode.Steps[i].Reward + DiscountFactor * runningReturn;
            returns[i] = runningReturn;
        }

        double mean = returns.Average();
        double std = Math.Sqrt(returns.Select(r => (r - mean) * (r - mean)).Average());
        double totalReward = episode.Steps.Sum(s => s.Reward);

        var logProbs = episode.Steps
            .Select(s => s.LogProbability ?? throw new InvalidOperationException(
                "policy-gradient episode step missing a log-probability -- RlEpisodeRunner should always attach one for an IPolicyGradientBrain"))
            .ToArray();

        var normalizedReturns = tensor(returns.Select(r => (float)((r - mean) / (std + 1e-8))).ToArray())
            .to(logProbs[0].device);

        var loss = -(stack(logProbs) * normalizedReturns).mean();

        optimizer.zero_grad();
        loss.backward();
        optimizer.step();

        foreach (var logProb in logProbs)
            logProb.Dispose();

        return totalReward;
    }
}
