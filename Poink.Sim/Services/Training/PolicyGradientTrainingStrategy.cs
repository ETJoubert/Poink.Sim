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
	private const double InitialEntropyCoefficient = 0.01;

	public string Name => "Policy Gradient (REINFORCE)";

	public bool CanTrain(ISimulationBrain brain) => brain is IPolicyGradientBrain;

	public optim.Optimizer CreateOptimizer(ISimulationBrain brain, double learningRate)
	{
		if (brain is not IPolicyGradientBrain policyBrain)
			throw new InvalidOperationException($"{Name} cannot train brain '{brain.Name}'");
		return optim.Adam(policyBrain.Network.parameters(), lr: learningRate);
	}

	public (double Reward, NeatGenerationStats? CompletedGeneration) TrainEpisode(
		ISimulationBrain brain,
		optim.Optimizer optimizer,
		RlEpisode episode,
		double progress
	)
	{
		if (brain is not IPolicyGradientBrain)
			throw new InvalidOperationException($"{Name} cannot train brain '{brain.Name}'");
		if (episode.Steps.Count == 0)
			return (0, null);

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

		var logProbs = episode
			.Steps.Select(s =>
				s.LogProbability
				?? throw new InvalidOperationException(
					"policy-gradient episode step missing a log-probability -- RlEpisodeRunner should always attach one for an IPolicyGradientBrain"
				)
			)
			.ToArray();
		var entropies = episode
			.Steps.Select(s =>
				s.Entropy
				?? throw new InvalidOperationException(
					"policy-gradient episode step missing an entropy -- RlEpisodeRunner should always attach one for an IPolicyGradientBrain"
				)
			)
			.ToArray();

		var normalizedReturns = tensor(returns.Select(r => (float)((r - mean) / (std + 1e-8))).ToArray())
			.to(logProbs[0].device);

		double entropyCoefficient = InitialEntropyCoefficient * (1.0 - Math.Clamp(progress, 0.0, 1.0));
		var meanEntropy = stack(entropies).mean();

		var loss = -(stack(logProbs) * normalizedReturns).mean() - meanEntropy * entropyCoefficient;

		optimizer.zero_grad();
		loss.backward();
		optimizer.step();

		foreach (var logProb in logProbs)
			logProb.Dispose();
		foreach (var entropy in entropies)
			entropy.Dispose();

		return (totalReward, null);
	}
}
