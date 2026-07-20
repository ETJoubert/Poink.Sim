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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Poink.Sim.Models;
using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Poink.Sim.Brain.Neural;

// One named value out of Encode(observation)'s raw input, purely for display
// (e.g. the Train page's "what goes in" panel) -- never used for inference.
internal sealed record RlInputFeature(string Label, double Value);

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

    // Labeled breakdown of Encode(observation) -- each brain knows its own
    // feature layout, so it's the one that knows how to describe it. Used to
    // show "what goes in" on the Train page; a future brain with a different
    // feature layout just describes its own.
    IReadOnlyList<RlInputFeature> DescribeInputs(SimulationObservation observation);

    // Serializes this brain's live weights via TorchSharp's own state-dict
    // binary format (Module.save/load's Stream overloads -- no temp file
    // needed) so a caller (IBrainRepository) can persist/restore them as a
    // Postgres bytea column. Default implementation, not brain-specific:
    // every IPolicyGradientBrain already exposes Network, and that's all
    // save/load need.
    byte[] ExportWeights()
    {
        using var stream = new MemoryStream();
        Network.save(stream);
        return stream.ToArray();
    }

    void ImportWeights(byte[] weights)
    {
        using var stream = new MemoryStream(weights);
        Network.load(stream);
    }

    // Total scalar count across every parameter tensor -- shown alongside a
    // saved brain (see IBrainRepository) so "how big is this network" is
    // visible without deserializing its weights.
    long WeightCount() => Network.parameters().Sum(p => p.numel());

    // ── Device placement ─────────────────────────────────────────────────
    //  Today's brain (MlpPolicyBrain) is tiny enough that device barely
    //  matters, but a bigger future brain won't be: training's batched
    //  per-episode backward pass is the one place GPU actually pays off,
    //  while Simulation's tick-by-tick single-sample inference never gets
    //  big enough to amortize GPU dispatch overhead, no matter the brain's
    //  size -- so it always runs on CPU. RlTrainingService calls
    //  MoveToTrainingDevice() right before a run starts; SimulationEngine
    //  calls MoveToInferenceDevice() right before a load finishes.
    //
    //  Neither call is safe to run concurrently with the other on the same
    //  live brain instance (relocating tensors while another thread is
    //  mid-forward/backward on them is a native-memory race, not just a
    //  "weights changed under you" cosmetic one) -- callers only invoke
    //  these at a run's start, and this app's usage is train-then-simulate,
    //  never both at once on the same brain, so no extra locking is added
    //  here for that case.
    void MoveToTrainingDevice() => Network.to(cuda.is_available() ? CUDA : CPU);

    void MoveToInferenceDevice() => Network.to(CPU);
}
