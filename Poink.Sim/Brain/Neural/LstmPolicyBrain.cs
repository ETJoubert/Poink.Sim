// ── Second policy-gradient brain: LSTM over the price window + capital/
//  position ratios ────────────────────────────────────────────────────────
//  Same feature contract as MlpPolicyBrain (same window, same capital
//  scalars, see Encode() below) -- the difference is entirely in how the
//  network consumes it: the price window is fed in as a true sequence (an
//  LSTM, hidden state carried step-to-step) instead of flattened into one
//  vector. This is the "fancier" brain MlpPolicyBrain's doc comment said
//  would come later -- meaningfully more parameters, and the first brain
//  where GPU during training (see IPolicyGradientBrain's device-placement
//  doc comment) actually has room to pay off.
//
//  Network is a hand-written Module<Tensor,Tensor> (LstmPolicyNetwork,
//  below), not a Sequential like MlpPolicyBrain's -- TorchSharp's
//  LSTM.forward returns an (output, h_n, c_n) tuple, not a single Tensor
//  Sequential can chain through, so forward() does the tuple-unpacking +
//  head itself. Encode() still returns one flat Tensor (window followed by
//  scalars, exactly like MlpPolicyBrain's), so IPolicyGradientBrain's
//  Network : Module<Tensor,Tensor> contract -- and everything built on it
//  (ExportWeights/ImportWeights/MoveToTrainingDevice/MoveToInferenceDevice/
//  WeightCount) -- needs no brain-specific overrides.
//
//  Each Decide() call starts the LSTM from a zero hidden/cell state (h0_c0:
//  null below) rather than carrying state across ticks -- consistent with
//  Encode()'s own "only this bounded window, no lookahead" contract, and
//  with Reset() being a no-op (there's no persistent recurrent state to
//  reset).
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;
using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Poink.Sim.Brain.Neural;

internal sealed class LstmPolicyBrain : IPolicyGradientBrain
{
    private const int WindowSize = 20;
    private const int ScalarFeatureCount = 4; // position direction, unrealized/equity, realized/equity, cash/equity
    private const int HiddenSize = 64;
    private const int NumLayers = 2;
    private const double CapitalFraction = 0.5;

    private readonly object _forwardLock = new();

    public LstmPolicyBrain()
    {
        // No explicit device placement -- see MlpPolicyBrain's constructor
        // comment; the same "start on CPU, move on demand" reasoning applies.
        Network = new LstmPolicyNetwork("lstm_policy", WindowSize, ScalarFeatureCount, HiddenSize, NumLayers, actionCount: 4);
    }

    public string Name => "LSTM Policy";

    public Module<Tensor, Tensor> Network { get; }

    private Device CurrentDevice => Network.parameters().First().device;

    // Weights persist across runs deliberately -- see MlpPolicyBrain's Reset().
    public void Reset() { }

    // Identical feature engineering to MlpPolicyBrain.Encode() -- see this
    // class's doc comment on why the network, not the features, is what
    // differs between the two brains.
    public Tensor Encode(SimulationObservation observation)
    {
        double currentPrice = observation.CurrentPrice;
        double[] prices = observation.RecentBars.Count > 0
            ? observation.RecentBars.Select(b => b.Close).ToArray()
            : observation.RecentTicks.Select(t => (t.Bid + t.Ask) / 2.0).ToArray();

        var features = new float[WindowSize + ScalarFeatureCount];

        int available = Math.Min(WindowSize, prices.Length);
        int padding = WindowSize - available;
        for (int i = 0; i < available; i++)
        {
            double price = prices[prices.Length - available + i];
            features[padding + i] = currentPrice == 0 ? 0f : (float)((price - currentPrice) / currentPrice);
        }

        var capital = observation.Capital;
        double equity = capital.Equity;
        features[WindowSize + 0] = observation.Position is { } position
            ? (position.Direction == PositionDirection.Long ? 1f : -1f)
            : 0f;
        features[WindowSize + 1] = equity != 0 ? (float)(capital.UnrealizedPnl / equity) : 0f;
        features[WindowSize + 2] = equity != 0 ? (float)(capital.RealizedPnl / equity) : 0f;
        features[WindowSize + 3] = equity != 0 ? (float)(capital.Cash / equity) : 0f;

        return tensor(features).unsqueeze(0).to(CurrentDevice);
    }

    public IReadOnlyList<RlInputFeature> DescribeInputs(SimulationObservation observation)
    {
        lock (_forwardLock)
        {
            using var scope = NewDisposeScope();
            using var noGradGuard = no_grad();
            using var input = Encode(observation);

            var features = new List<RlInputFeature>(WindowSize + ScalarFeatureCount);
            for (int i = 0; i < WindowSize; i++)
                features.Add(new RlInputFeature($"t-{WindowSize - i}", input[0, i].item<float>()));
            features.Add(new RlInputFeature("position", input[0, WindowSize + 0].item<float>()));
            features.Add(new RlInputFeature("unrealized/eq", input[0, WindowSize + 1].item<float>()));
            features.Add(new RlInputFeature("realized/eq", input[0, WindowSize + 2].item<float>()));
            features.Add(new RlInputFeature("cash/eq", input[0, WindowSize + 3].item<float>()));
            return features;
        }
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

    public (SimulationDecision Decision, Tensor ActionLogProbability) DecideWithLogProbability(SimulationObservation observation)
    {
        lock (_forwardLock)
        {
            var input = Encode(observation);
            var logits = Network.forward(input);
            var logProbs = functional.log_softmax(logits, dim: 1);
            int actionIndex = SampleActionIndex(exp(logProbs));
            var actionLogProbability = logProbs[0, actionIndex];
            return (BuildDecision(actionIndex), actionLogProbability);
        }
    }

    private static int SampleActionIndex(Tensor actionProbabilities)
    {
        using var sampled = multinomial(actionProbabilities, 1);
        return (int)sampled.item<long>();
    }

    private static SimulationDecision BuildDecision(int actionIndex) => actionIndex switch
    {
        0 => new SimulationDecision(SimulationAction.Hold),
        1 => new SimulationDecision(SimulationAction.EnterLong, CapitalFraction, Reason: "policy"),
        2 => new SimulationDecision(SimulationAction.EnterShort, CapitalFraction, Reason: "policy"),
        _ => new SimulationDecision(SimulationAction.Close, Reason: "policy"),
    };
}

// The actual recurrent network -- LSTM over the price-window slice of
// Encode()'s flat feature vector; its final hidden state (top layer) is
// concatenated with the trailing capital scalars and passed through a small
// MLP head to produce one logit per SimulationAction. Hand-written (not
// Sequential) since LSTM.forward returns a tuple -- see LstmPolicyBrain's
// doc comment.
internal sealed class LstmPolicyNetwork : Module<Tensor, Tensor>
{
    private readonly long _windowSize;
    private readonly LSTM _lstm;
    private readonly Linear _head1;
    private readonly Linear _head2;

    public LstmPolicyNetwork(string name, long windowSize, long scalarCount, long hiddenSize, long numLayers, long actionCount)
        : base(name)
    {
        _windowSize = windowSize;
        _lstm = nn.LSTM(inputSize: 1, hiddenSize: hiddenSize, numLayers: numLayers, batchFirst: true);
        _head1 = nn.Linear(hiddenSize + scalarCount, 32);
        _head2 = nn.Linear(32, actionCount);

        // TorchSharp's equivalent of PyTorch's automatic submodule discovery
        // via self.foo = ... in __init__ -- required for this module's own
        // .parameters()/.to()/.save()/.load() to traverse _lstm/_head1/_head2
        // (that's what makes MoveToTrainingDevice/ExportWeights/etc. on
        // IPolicyGradientBrain work unmodified for this brain).
        RegisterComponents();
    }

    public override Tensor forward(Tensor input)
    {
        var window = input.narrow(1, 0, _windowSize).unsqueeze(-1); // (batch, windowSize, 1)
        var scalars = input.narrow(1, _windowSize, input.shape[1] - _windowSize); // (batch, scalarCount)

        var (_, hn, _) = _lstm.forward(window, null); // null h0_c0 -- fresh zero state per call, see class doc comment
        var lastHidden = hn[-1]; // (batch, hiddenSize) -- top layer's final hidden state

        var combined = cat(new[] { lastHidden, scalars }, dim: 1);
        var hidden = functional.tanh(_head1.forward(combined));
        return _head2.forward(hidden);
    }
}
