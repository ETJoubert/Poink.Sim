// ── RL algorithm abstraction: "different training strategies" ───────────
//  Kept stateless/pure like Poink.ML's brain interfaces (a strategy is just
//  a factory + an episode-to-gradient-step function) -- RlTrainingService
//  owns the optimizer's lifetime across a whole training run, so Adam's
//  momentum/variance state persists correctly across episodes instead of
//  resetting every episode. Adding a new algorithm (e.g. a future DQN
//  strategy) means adding a class and one DI registration line, nothing else
//  changes -- same expansion pattern as adding a new ISimulationBrain.
// ─────────────────────────────────────────────────────────────────────────
using Poink.Sim.Brain;
using static TorchSharp.torch;

namespace Poink.Sim.Training;

internal interface IRlTrainingStrategy
{
    string Name { get; }

    // Whether this strategy knows how to train the given brain (e.g. a
    // policy-gradient strategy only recognizes IPolicyGradientBrain).
    bool CanTrain(ISimulationBrain brain);

    optim.Optimizer CreateOptimizer(ISimulationBrain brain, double learningRate);

    // Turns one recorded episode into one gradient step. Returns a metric
    // (total episode reward) for progress reporting.
    double TrainEpisode(ISimulationBrain brain, optim.Optimizer optimizer, RlEpisode episode);
}
