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
using System.Reflection;
using Poink.Sim.Models;
using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Poink.Sim.Brain.Neural;

// One named value out of Encode(observation)'s raw input, purely for display
// (e.g. the Train page's "what goes in" panel) -- never used for inference.
internal sealed record RlInputFeature(string Label, double Value);

// One "layer" in the Train page's ambient Network Activity animation. Units
// is a real magnitude (a Linear's out_features, or an LSTM's hidden size),
// not meant to be drawn one-dot-per-unit -- see networkEmulator.js for how
// it's scaled down for legibility. IsRecurrent marks a layer that loops over
// a sequence (an LSTM) rather than a plain feedforward one, so the animation
// can draw it differently. Public (not internal like most of this file)
// because it's passed as a [Parameter] into NetworkEmulator (see
// RlEpisodeMetric's doc comment for why).
public sealed record NetworkLayerShape(int Units, bool IsRecurrent);

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

    // Walks Network's own module tree (TorchSharp's named_modules(), the
    // library's own reflection-ish traversal) to describe its shape for the
    // Train page's Network Activity animation, instead of a shape hand-
    // maintained per brain -- a differently-shaped future brain draws itself
    // correctly here with zero changes. The first Linear/LSTM encountered
    // also contributes its input width, so the very first visualized layer
    // shows "what feeds in", not just "what comes out of the first op".
    //
    // LSTM specifically needs real System.Reflection on top of that: unlike
    // Linear (which exposes in_features/out_features publicly), TorchSharp's
    // LSTM only stores hidden_size/num_layers in private fields with no
    // public accessor -- ReadLstmHiddenSize below is the one place this
    // interface reaches past TorchSharp's public API.
    IReadOnlyList<NetworkLayerShape> DescribeNetworkShape()
    {
        var shapes = new List<NetworkLayerShape>();
        foreach (var (_, module) in Network.named_modules())
        {
            switch (module)
            {
                case Linear linear:
                    if (shapes.Count == 0)
                        shapes.Add(new NetworkLayerShape((int)linear.in_features, IsRecurrent: false));
                    shapes.Add(new NetworkLayerShape((int)linear.out_features, IsRecurrent: false));
                    break;
                case LSTM lstm:
                    shapes.Add(new NetworkLayerShape(ReadLstmHiddenSize(lstm), IsRecurrent: true));
                    break;
            }
        }
        return shapes;
    }

    private static readonly FieldInfo? LstmHiddenSizeField =
        typeof(LSTM).GetField("_hidden_size", BindingFlags.NonPublic | BindingFlags.Instance);

    private static int ReadLstmHiddenSize(LSTM lstm) =>
        LstmHiddenSizeField?.GetValue(lstm) is long hiddenSize ? (int)hiddenSize : 0;
}
