using System;
using System.Linq;
using Poink.Sim.Brain;
using Poink.Sim.Brain.Neural;
using TorchSharp;
using static TorchSharp.torch;

namespace Poink.Sim.Training;

internal sealed class PerDecisionPolicyGradientTrainingStrategy : IRlTrainingStrategy
{
	private const double DiscountFactor = 0.99;
	private const double InitialEntropyCoefficient = 0.01;

	public string Name => "Policy Gradient (Per-Decision)";

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
		if (brain is not IPolicyGradientBrain policyBrain)
			throw new InvalidOperationException($"{Name} cannot train brain '{brain.Name}'");
		if (episode.Steps.Count == 0)
			return (0, null);

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

		double entropyCoefficient = InitialEntropyCoefficient * (1.0 - Math.Clamp(progress, 0.0, 1.0));

		for (int i = 0; i < episode.Steps.Count; i++)
		{
			var step = episode.Steps[i];
			var replay =
				step.Replay
				?? throw new InvalidOperationException(
					"policy-gradient episode step missing replay state -- RlEpisodeRunner should always attach one for an IPolicyGradientBrain"
				);

			// Recomputed fresh each iteration -- reusing rollout-time tensors here caused an
			// in-place modification error, since every step shares the same Network parameters.
			using (NewDisposeScope())
			{
				var (logProbability, entropy) = policyBrain.Recompute(replay);

				double normalizedReturn = (returns[i] - mean) / (std + 1e-8);
				var loss = -(logProbability * (float)normalizedReturn) - entropy * entropyCoefficient;

				optimizer.zero_grad();
				loss.backward();
				optimizer.step();
			}

			replay.Dispose();
			step.LogProbability?.Dispose();
			step.Entropy?.Dispose();
		}

		return (totalReward, null);
	}
}
