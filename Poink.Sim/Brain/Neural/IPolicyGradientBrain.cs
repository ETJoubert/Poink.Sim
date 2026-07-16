// ── Trained-brain family: stochastic-policy neural networks ─────────────
//  A sibling to ISimulationBrain, not a replacement -- mirrors Poink.ML's
//  choice of one narrow interface per model family (IForecastModel /
//  IQuantileBrain / IPatternBrain) rather than one do-everything abstraction.
//  A future value-based brain (e.g. DQN) would get its own sibling interface
//  later instead of being force-fit into this one.
//
//  Decide() (from ISimulationBrain) is satisfied by discarding the log-prob
//  from DecideWithLogProbability, so a policy-gradient brain drops into
//  SimulationEngine/the existing Brain dropdown with zero engine changes --
//  only a training strategy needs the extra members below.
// ─────────────────────────────────────────────────────────────────────────
using Poink.Sim.Models;
using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Poink.Sim.Brain.Neural;

internal interface IPolicyGradientBrain : ISimulationBrain
{
    // The live module. A training strategy mutates these weights in place --
    // the same instance used for live simulation playback, so it visibly
    // improves as training progresses (no separate "trained copy" to promote).
    Module<Tensor, Tensor> Network { get; }

    // This brain's own fixed-width feature encoding -- the only data path in
    // from SimulationObservation, keeping it honest to "only sees what the
    // trader sees" (no lookahead, nothing beyond what ISimulationBrain.Decide
    // already receives).
    Tensor Encode(SimulationObservation observation);

    // Forward pass + categorical sample + log-prob of the sampled action.
    // The log-prob is what a policy-gradient training strategy needs to build
    // its loss; plain simulation playback only needs the Decision half (see
    // Decide() above).
    (SimulationDecision Decision, Tensor ActionLogProbability) DecideWithLogProbability(SimulationObservation observation);
}
