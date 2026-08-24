using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Poink.Sim.Brain;
using Poink.Sim.Brain.Neural;
using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;

namespace Poink.Sim.Training;

internal sealed class NeatTrainingStrategy(ILogger<NeatTrainingStrategy> logger) : IRlTrainingStrategy
{
	public string Name => "NEAT";

	public bool CanTrain(ISimulationBrain brain) => brain is INeatPolicyBrain;

	public optim.Optimizer CreateOptimizer(ISimulationBrain brain, double learningRate)
	{
		if (brain is not INeatPolicyBrain)
			throw new InvalidOperationException($"{Name} cannot train brain '{brain.Name}'");

		// NEAT has no gradients -- a zero-parameter optimizer is inert by construction and
		// satisfies IRlTrainingStrategy's contract without a bespoke no-op type.
		return optim.SGD(Array.Empty<Parameter>(), learningRate);
	}

	public (double Reward, NeatGenerationStats? CompletedGeneration) TrainEpisode(
		ISimulationBrain brain,
		optim.Optimizer optimizer,
		RlEpisode episode,
		double progress
	)
	{
		if (brain is not INeatPolicyBrain neatBrain)
			throw new InvalidOperationException($"{Name} cannot train brain '{brain.Name}'");

		double fitness = episode.Steps.Sum(s => s.Reward);
		neatBrain.Population.ReportFitness(fitness);

		NeatGenerationStats? completedGeneration = null;
		if (!neatBrain.Population.TryAdvanceToNextGenome())
		{
			completedGeneration = neatBrain.Population.Evolve();
			logger.LogInformation(
				"NEAT generation {Generation} complete for {Brain}: population={PopulationSize}, species={SpeciesCount}, bestFitness={BestFitness:N2}, avgFitness={AverageFitness:N2}",
				completedGeneration.Generation,
				brain.Name,
				completedGeneration.PopulationSize,
				completedGeneration.SpeciesCount,
				completedGeneration.BestFitness,
				completedGeneration.AverageFitness
			);
		}

		return (fitness, completedGeneration);
	}
}
