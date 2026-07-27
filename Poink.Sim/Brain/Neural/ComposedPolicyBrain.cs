// ── The one generic policy-gradient brain: encoder + architecture ───────
//  Replaces what used to be 4 separate classes (MlpPolicyBrain,
//  LstmPolicyBrain, VolumeProfilePolicyBrain, VolumeProfileBarsPolicyBrain),
//  each duplicating the same Decide/DecideWithLogProbability/
//  SampleActionIndex/BuildDecision boilerplate around its own hardwired
//  Encode+Network pair. A ComposedPolicyBrain instead holds an
//  IObservationEncoder (built from one or more parts via CompositeEncoder)
//  and a Network built by an INetworkArchitecture from that encoder's
//  FeatureCount/SequenceLength -- see Create below, and Program.cs for how
//  the 4 original brains are now expressed as one line each.
//
//  Encode wraps Encoder.Encode's plain float[] into a Tensor here (the one
//  place device placement happens) -- encoders themselves never touch
//  TorchSharp/autograd/device at all.
//
//  DescribeInputs is a trivial zip of Encoder.Features with
//  Encoder.Encode(observation) -- deliberately NOT wrapped in a lock/
//  no_grad/NewDisposeScope like the 4 originals' DescribeInputs were, since
//  it never touches Network or autograd (encoding alone never builds a
//  graph). This is a simplification enabled by moving Encode off Tensor,
//  not an oversight.
//
//  Not sealed -- the one deliberate deviation from this folder's otherwise-
//  universal "internal sealed class" convention, solely so LookbackAwarePolicyBrain
//  (below) can extend it to surface IHasLookbackBars only when one or more of
//  the encoder parts actually needs it (see Create's is-check) rather than
//  every composed brain always implementing it. The constructor is private
//  protected so nothing else can subclass it.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;
using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Poink.Sim.Brain.Neural;

internal class ComposedPolicyBrain : IPolicyGradientBrain
{
    private const double CapitalFraction = 0.5;

    // Canonical order + default (every direction enabled, today's original
    // behavior) -- BuildDecision/DescribeOutputs iterate whatever subset a
    // brain was built with in this same fixed order, so e.g. a brain missing
    // Hold still lists EnterLong/EnterShort/Close in this relative order.
    public static readonly IReadOnlyList<SimulationAction> AllDirections =
        [SimulationAction.Hold, SimulationAction.EnterLong, SimulationAction.EnterShort, SimulationAction.Close];

    private readonly object _forwardLock = new();
    private readonly IObservationEncoder _encoder;
    private readonly IReadOnlyList<IActionOutputHead> _actionOutputHeads;
    private readonly IReadOnlyList<SimulationAction> _enabledDirections;

    private protected ComposedPolicyBrain(
        string name, IObservationEncoder encoder, Module<Tensor, Tensor> network,
        IReadOnlyList<IActionOutputHead> actionOutputHeads, IReadOnlyList<SimulationAction> enabledDirections)
    {
        Name = name;
        _encoder = encoder;
        Network = network;
        _actionOutputHeads = actionOutputHeads;
        _enabledDirections = enabledDirections;
    }

    // Builds a brain from an architecture plus one or more encoder parts
    // (concatenated via CompositeEncoder, see that class's ordering
    // constraint). If one or more parts implement IHasLookbackBars, the
    // returned brain surfaces that capability too (via LookbackAwarePolicyBrain,
    // which broadcasts one configured LookbackBars value to every one of
    // them) so RlTrainingService's `brain is IHasLookbackBars` check stays
    // precise -- absent unless at least one part actually uses it. Lets e.g.
    // VolumeProfileEncoder and TimeProfileEncoder be combined in one brain
    // and share the same trailing-bar window instead of needing separate
    // lookback settings.
    //
    // actionOutputHeads: zero or more extra (non-direction) decisions this
    // brain's network also makes -- see IActionOutputHead. enabledDirections:
    // which of Hold/EnterLong/EnterShort/Close this brain's direction pick is
    // narrowed to -- like actionOutputHeads, every one of these is optional
    // per brain (see SimulationAction's doc comment), not just the added
    // heads; defaults to AllDirections (today's original always-4 behavior)
    // so a direction-only brain (e.g. Program.cs's fixed brains) doesn't need
    // to pass anything.
    public static IPolicyGradientBrain Create(
        string name, INetworkArchitecture architecture, IReadOnlyList<IActionOutputHead> actionOutputHeads,
        IReadOnlyList<SimulationAction> enabledDirections, params IObservationEncoder[] parts)
    {
        if (enabledDirections.Count == 0)
            throw new ArgumentException("a brain needs at least one enabled direction (Hold/EnterLong/EnterShort/Close)", nameof(enabledDirections));

        var encoder = CompositeEncoder.Create(parts);
        int extraOutputWidth = actionOutputHeads.Sum(h => h.OutputWidth);
        var network = architecture.Build(encoder.FeatureCount, encoder.SequenceLength, enabledDirections.Count + extraOutputWidth);

        var lookbackParts = parts.OfType<IHasLookbackBars>().ToList();

        return lookbackParts.Count > 0
            ? new LookbackAwarePolicyBrain(name, encoder, network, lookbackParts, actionOutputHeads, enabledDirections)
            : new ComposedPolicyBrain(name, encoder, network, actionOutputHeads, enabledDirections);
    }

    public static IPolicyGradientBrain Create(
        string name, INetworkArchitecture architecture, IReadOnlyList<IActionOutputHead> actionOutputHeads, params IObservationEncoder[] parts) =>
        Create(name, architecture, actionOutputHeads, AllDirections, parts);

    public static IPolicyGradientBrain Create(string name, INetworkArchitecture architecture, params IObservationEncoder[] parts) =>
        Create(name, architecture, [], AllDirections, parts);

    public string Name { get; }

    public Module<Tensor, Tensor> Network { get; }

    public int WarmupBars => _encoder.WarmupBars;

    private Device CurrentDevice => Network.parameters().First().device;

    // Weights persist across runs deliberately -- resetting on every run
    // would undo whatever training has accumulated.
    public void Reset() { }

    public Tensor Encode(SimulationObservation observation) =>
        tensor(_encoder.Encode(observation)).unsqueeze(0).to(CurrentDevice);

    public IReadOnlyList<RlInputFeature> DescribeInputs(SimulationObservation observation)
    {
        var values = _encoder.Encode(observation);
        var descriptors = _encoder.Features;
        var features = new List<RlInputFeature>(values.Length);
        for (int i = 0; i < values.Length; i++)
            features.Add(new RlInputFeature(descriptors[i].Label, values[i], descriptors[i].Normalizer));
        return features;
    }

    public SimulationDecision Decide(SimulationObservation observation)
    {
        lock (_forwardLock)
        {
            using var scope = NewDisposeScope();
            using var noGradGuard = no_grad();
            return DecideWithLogProbability(observation).Decision;
        }
    }

    public (SimulationDecision Decision, Tensor ActionLogProbability, IReadOnlyList<RlOutputFeature> Outputs) DecideWithLogProbability(
        SimulationObservation observation)
    {
        lock (_forwardLock)
        {
            var input = Encode(observation);
            var logits = Network.forward(input);

            var directionLogits = logits.narrow(1, 0, _enabledDirections.Count);
            var directionLogProbs = functional.log_softmax(directionLogits, dim: 1);
            var directionProbabilities = exp(directionLogProbs);
            int actionIndex = CategoricalSampling.SampleIndex(directionProbabilities);
            var actionLogProbability = directionLogProbs[0, actionIndex];
            var outputs = new List<RlOutputFeature>(DescribeOutputs(directionProbabilities, actionIndex));

            var headValues = new Dictionary<ActionOutputKind, double?>();
            int offset = _enabledDirections.Count;
            foreach (var head in _actionOutputHeads)
            {
                var slice = logits.narrow(1, offset, head.OutputWidth);
                var (value, logProbability, headOutputs) = head.Sample(slice);
                headValues[head.Kind] = value;
                actionLogProbability = actionLogProbability + logProbability;
                outputs.AddRange(headOutputs);
                offset += head.OutputWidth;
            }

            return (BuildDecision(_enabledDirections[actionIndex], headValues, observation.CurrentPrice), actionLogProbability, outputs);
        }
    }

    private IReadOnlyList<RlOutputFeature> DescribeOutputs(Tensor actionProbabilities, int chosenIndex)
    {
        var outputs = new List<RlOutputFeature>(_enabledDirections.Count);
        for (int i = 0; i < _enabledDirections.Count; i++)
            outputs.Add(new RlOutputFeature(_enabledDirections[i].ToString(), actionProbabilities[0, i].item<float>(), i == chosenIndex, Group: "Direction"));
        return outputs;
    }

    // headValues holds this decision's sampled Volume/StopLoss/TakeProfit
    // values (real units -- capital fraction 0..1, percent-distance-from-
    // price) for whichever ActionOutputKinds this brain was built with;
    // absent for any kind the brain wasn't built with, in which case the
    // fixed CapitalFraction/no-SL-TP fallback below applies -- same
    // treatment a direction-only brain has always had. StopLoss/TakeProfit
    // percent-distances convert to absolute prices the same way
    // RandomBrain.Decide already does. action is whichever SimulationAction
    // _enabledDirections[actionIndex] resolved to -- switching on the actual
    // enum value rather than a raw index, since a brain missing e.g. Hold
    // no longer has index 0 mean Hold.
    private static SimulationDecision BuildDecision(SimulationAction action, IReadOnlyDictionary<ActionOutputKind, double?> headValues, double currentPrice)
    {
        double capitalFraction = headValues.TryGetValue(ActionOutputKind.Volume, out var volume) && volume is { } v ? v : CapitalFraction;
        double? stopLossPercent = headValues.TryGetValue(ActionOutputKind.StopLoss, out var sl) ? sl : null;
        double? takeProfitPercent = headValues.TryGetValue(ActionOutputKind.TakeProfit, out var tp) ? tp : null;

        return action switch
        {
            SimulationAction.Hold => new SimulationDecision(SimulationAction.Hold),
            SimulationAction.EnterLong => new SimulationDecision(SimulationAction.EnterLong, capitalFraction,
                    StopLossPrice: stopLossPercent is { } slLong ? currentPrice * (1 - slLong) : null,
                    TakeProfitPrice: takeProfitPercent is { } tpLong ? currentPrice * (1 + tpLong) : null,
                    Reason: "policy"),
            SimulationAction.EnterShort => new SimulationDecision(SimulationAction.EnterShort, capitalFraction,
                    StopLossPrice: stopLossPercent is { } slShort ? currentPrice * (1 + slShort) : null,
                    TakeProfitPrice: takeProfitPercent is { } tpShort ? currentPrice * (1 - tpShort) : null,
                    Reason: "policy"),
            _ => new SimulationDecision(SimulationAction.Close, Reason: "policy"),
        };
    }
}

// Surfaces IHasLookbackBars only when one or more of the composed encoder's
// parts actually implement it -- see ComposedPolicyBrain.Create. Setting
// LookbackBars broadcasts the same value to every wrapped part, so e.g. a
// VolumeProfileEncoder and TimeProfileEncoder combined in one brain always
// merge over the same trailing-bar window; getting it just reads the first
// (all parts are kept in sync by the setter, so any would do).
internal sealed class LookbackAwarePolicyBrain : ComposedPolicyBrain, IHasLookbackBars
{
    private readonly IReadOnlyList<IHasLookbackBars> _lookbackParts;

    internal LookbackAwarePolicyBrain(
        string name, IObservationEncoder encoder, Module<Tensor, Tensor> network,
        IReadOnlyList<IHasLookbackBars> lookbackParts, IReadOnlyList<IActionOutputHead> actionOutputHeads,
        IReadOnlyList<SimulationAction> enabledDirections)
        : base(name, encoder, network, actionOutputHeads, enabledDirections) => _lookbackParts = lookbackParts;

    public int LookbackBars
    {
        get => _lookbackParts[0].LookbackBars;
        set { foreach (var part in _lookbackParts) part.LookbackBars = value; }
    }
}
