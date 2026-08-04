
using Poink.Sim.Brain;
using static TorchSharp.torch;

namespace Poink.Sim.Training;

internal interface IRlTrainingStrategy
{
    string Name { get; }


    bool CanTrain(ISimulationBrain brain);

    optim.Optimizer CreateOptimizer(ISimulationBrain brain, double learningRate);


    double TrainEpisode(ISimulationBrain brain, optim.Optimizer optimizer, RlEpisode episode, double progress);
}
