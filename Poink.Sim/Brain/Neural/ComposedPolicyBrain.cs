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

    private readonly object _forwardLock = new();
    private readonly IObservationEncoder _encoder;

    private protected ComposedPolicyBrain(string name, IObservationEncoder encoder, Module<Tensor, Tensor> network)
    {
        Name = name;
        _encoder = encoder;
        Network = network;
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
    public static IPolicyGradientBrain Create(string name, INetworkArchitecture architecture, params IObservationEncoder[] parts)
    {
        var encoder = CompositeEncoder.Create(parts);
        var network = architecture.Build(encoder.FeatureCount, encoder.SequenceLength);

        var lookbackParts = parts.OfType<IHasLookbackBars>().ToList();

        return lookbackParts.Count > 0
            ? new LookbackAwarePolicyBrain(name, encoder, network, lookbackParts)
            : new ComposedPolicyBrain(name, encoder, network);
    }

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
            var logProbs = functional.log_softmax(logits, dim: 1);
            var probabilities = exp(logProbs);
            int actionIndex = SampleActionIndex(probabilities);
            var actionLogProbability = logProbs[0, actionIndex];
            var outputs = DescribeOutputs(probabilities, actionIndex);
            return (BuildDecision(actionIndex), actionLogProbability, outputs);
        }
    }

    // actionIndex order matches BuildDecision's switch below -- both read the
    // same fixed Hold/EnterLong/EnterShort/Close layout the network's output
    // layer is trained against.
    private static readonly string[] ActionLabels = ["Hold", "EnterLong", "EnterShort", "Close"];

    private static IReadOnlyList<RlOutputFeature> DescribeOutputs(Tensor actionProbabilities, int chosenIndex)
    {
        var outputs = new List<RlOutputFeature>(ActionLabels.Length);
        for (int i = 0; i < ActionLabels.Length; i++)
            outputs.Add(new RlOutputFeature(ActionLabels[i], actionProbabilities[0, i].item<float>(), i == chosenIndex));
        return outputs;
    }

    // See MlpPolicyBrain's original SampleActionIndex doc comment (now
    // carried over here) -- native-crash hazard (native multinomial), same
    // guard, same manual-sampling fix. isfinite catches a NaN/Inf
    // probability (e.g. from a bad tick's price propagating through Encode,
    // or exploding gradients during training) as an ordinary managed
    // exception the training loop can surface as an Error, instead of an
    // unrecoverable native crash. Sampling itself is done manually
    // (cumulative sum vs. one random draw) rather than via TorchSharp's
    // native `multinomial` -- that op has been observed to crash the whole
    // process on this exact 4-category call with two different native
    // exceptions (a CUDA "dtype Bool" error, then a CPU "NYI" from an
    // internal pow), both triggered by an ordinary near-one-hot probability
    // vector (e.g. a confident policy) that isfinite doesn't (and shouldn't)
    // reject -- 0/1 are perfectly finite. Reading out a handful of floats
    // and walking a cumulative sum in managed code sidesteps that native op
    // entirely; cheap and exact for a 4-way action space.
    private static int SampleActionIndex(Tensor actionProbabilities)
    {
        if (!isfinite(actionProbabilities).all().item<bool>())
            throw new InvalidOperationException(
                "policy produced non-finite action probabilities (NaN/Inf) -- check input data and recent gradient updates for the cause");

        int actionCount = (int)actionProbabilities.shape[1];
        Span<float> probabilities = stackalloc float[actionCount];
        float total = 0;
        for (int i = 0; i < actionCount; i++)
        {
            probabilities[i] = actionProbabilities[0, i].item<float>();
            total += probabilities[i];
        }

        float threshold = (float)Random.Shared.NextDouble() * total;
        float cumulative = 0;
        for (int i = 0; i < actionCount; i++)
        {
            cumulative += probabilities[i];
            if (cumulative >= threshold) return i;
        }
        return actionCount - 1; // float-rounding safety net -- cumulative sum landed just under threshold
    }

    private static SimulationDecision BuildDecision(int actionIndex) => actionIndex switch
    {
        0 => new SimulationDecision(SimulationAction.Hold),
        1 => new SimulationDecision(SimulationAction.EnterLong, CapitalFraction, Reason: "policy"),
        2 => new SimulationDecision(SimulationAction.EnterShort, CapitalFraction, Reason: "policy"),
        _ => new SimulationDecision(SimulationAction.Close, Reason: "policy"),
    };
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

    internal LookbackAwarePolicyBrain(string name, IObservationEncoder encoder, Module<Tensor, Tensor> network, IReadOnlyList<IHasLookbackBars> lookbackParts)
        : base(name, encoder, network) => _lookbackParts = lookbackParts;

    public int LookbackBars
    {
        get => _lookbackParts[0].LookbackBars;
        set { foreach (var part in _lookbackParts) part.LookbackBars = value; }
    }
}
