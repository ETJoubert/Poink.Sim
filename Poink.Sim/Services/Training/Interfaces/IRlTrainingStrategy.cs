using Poink.Sim.Brain;
using Poink.Sim.Brain.Neural;
using static TorchSharp.torch;

namespace Poink.Sim.Training;

internal interface IRlTrainingStrategy
{
	string Name { get; }

	bool CanTrain(ISimulationBrain brain);

	optim.Optimizer CreateOptimizer(ISimulationBrain brain, double learningRate);

	/// <summary>
	/// CompletedGeneration is non-null only on the exact call where a generation-based
	/// strategy (NEAT) advances its population; episode-based strategies always return null.
	/// </summary>
	(double Reward, NeatGenerationStats? CompletedGeneration) TrainEpisode(
		ISimulationBrain brain,
		optim.Optimizer optimizer,
		RlEpisode episode,
		double progress
	);
}
