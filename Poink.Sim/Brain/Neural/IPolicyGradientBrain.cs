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
// Normalizer is the same IFeatureNormalizer that produced Value (see
// IObservationEncoder.Features), carried along so a display-only caller
// (BrainIoPanel.FormatInputValue) can invert it back to real units generically,
// without knowing this feature's label convention -- typed as the narrower
// IFeatureDisplayNormalizer (Denormalize/Format only, see that interface's
// doc comment) rather than the full IFeatureNormalizer, since the full
// interface's Normalize needs an internal SimulationObservation this record
// has no reason to require public. Public (not internal like most of this
// file) because it's passed as a [Parameter] into BrainIoPanel, shared
// between the Train and Simulation pages (see RlEpisodeMetric's doc comment
// for why a Razor component parameter can't be typed with a less-accessible
// type than the component itself).
public sealed record RlInputFeature(string Label, double Value, IFeatureDisplayNormalizer Normalizer);

// One choice's probability out of one output head's full distribution for
// one decision -- mirrors RlInputFeature but describes what the network
// decided rather than what fed it. Probabilities within one Group sum to
// ~1 (a brain has one "Direction" group plus one group per enabled
// IActionOutputHead, e.g. "Volume"/"StopLoss"/"TakeProfit" -- see
// BrainIoPanel, which renders each group as its own bar-set); IsChosen
// flags which one the (stochastic) sampler actually picked for this
// decision within its group, since the highest-probability choice isn't
// necessarily the one taken. Label is the SimulationAction member name
// ("Hold"/"EnterLong"/"EnterShort"/"Close") for the Direction group, or a
// DiscretizedLevelsActionHead level label ("25%"/"Tight"/...) for the
// others. Public for the same BrainIoPanel reason as RlInputFeature above.
public sealed record RlOutputFeature(string Label, double Probability, bool IsChosen, string Group = "Direction");

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

    // How many trailing real bars RlEpisodeRunner should consume to build up
    // history before this brain's first decision -- default 0 (no warm-up)
    // for a brain with no windowed encoder. ComposedPolicyBrain overrides
    // this to forward its encoder's own WarmupBars (see
    // IObservationEncoder.WarmupBars); a hand-written IPolicyGradientBrain
    // with no notion of a lookback window is fine leaving this at 0.
    int WarmupBars => 0;

    // Forward pass + categorical sample + log-prob of the sampled action.
    // The log-prob is what a policy-gradient training strategy needs to build
    // its loss; plain simulation playback only needs the Decision half (see
    // Decide() above). Outputs is the same forward pass's full per-action
    // distribution, labeled -- free to compute here (log_softmax already
    // produces it before sampling collapses it to one index), so the Train
    // page's "what the network decided" panel doesn't need a second forward
    // pass (and re-sampling via a second call would risk a different action).
    // Entropy is the direction distribution's own entropy (-sum(p*log p)),
    // computed from the same log_softmax output before sampling -- unlike
    // ActionLogProbability, it's a direct differentiable function of the
    // logits (no sampled index involved), which is what lets a training
    // strategy add an entropy bonus to its loss to discourage the policy
    // collapsing onto one action before it's found real signal.
    (SimulationDecision Decision, Tensor ActionLogProbability, Tensor Entropy, IReadOnlyList<RlOutputFeature> Outputs) DecideWithLogProbability(
        SimulationObservation observation);

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
    // correctly here with zero changes, as long as it's built from a module
    // type already handled below. The first Linear/Conv1d/LSTM encountered
    // also contributes its input width/channels, so the very first
    // visualized layer shows "what feeds in", not just "what comes out of
    // the first op". Conv1d's out_channels stands in for a conv layer's
    // "width" the same way Linear's out_features does -- CnnPolicyNetwork's
    // _convStack is a Sequential, so named_modules() walks straight into its
    // individual Conv1d children with no extra case needed for the
    // Sequential wrapper itself.
    //
    // LSTM specifically needs real System.Reflection on top of that: unlike
    // Linear/Conv1d (which expose in_features/out_features and
    // in_channels/out_channels publicly), TorchSharp's LSTM only stores
    // hidden_size/num_layers in private fields with no public accessor --
    // ReadLstmHiddenSize below is the one place this interface reaches past
    // TorchSharp's public API.
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
                case Conv1d conv1d:
                    if (shapes.Count == 0)
                        shapes.Add(new NetworkLayerShape((int)conv1d.in_channels, IsRecurrent: false));
                    shapes.Add(new NetworkLayerShape((int)conv1d.out_channels, IsRecurrent: false));
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
