// ── First concrete policy-gradient brain: small MLP over a price window +
//  capital/position ratios ────────────────────────────────────────────────
//  Same "flatten everything into one MLP" shape Poink.ML uses as its
//  reference architecture (MlpQuantileBrain/MlpForecastModel) before fancier
//  ones get added later. Network is built once in the constructor and kept
//  alive for this object's lifetime -- weights persist across simulation
//  runs, and a training strategy mutates them in place (see
//  IPolicyGradientBrain's doc comment) rather than swapping in a new Module.
//
//  Decide() wraps the whole forward pass in NewDisposeScope()+no_grad() since
//  nothing needs to escape it (only a plain SimulationDecision is returned);
//  DecideWithLogProbability leaves scoping to its caller, since a training
//  run needs the returned log-prob tensor to survive until backward() runs
//  at episode end (see Training/RlEpisodeRunner.cs).
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;
using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Poink.Sim.Brain.Neural;

internal sealed class MlpPolicyBrain : IPolicyGradientBrain
{
    private const int WindowSize = 20;
    private const int ScalarFeatureCount = 4; // position direction, unrealized/equity, realized/equity, cash/equity
    private const int FeatureCount = WindowSize + ScalarFeatureCount;
    private const double CapitalFraction = 0.5;

    private readonly object _forwardLock = new();
    private readonly Device _device;

    public MlpPolicyBrain()
    {
        _device = cuda.is_available() ? CUDA : CPU;
        Network = Sequential(
            ("hidden1", Linear(FeatureCount, 32)),
            ("fold1", Tanh()),
            ("hidden2", Linear(32, 16)),
            ("fold2", Tanh()),
            ("heads", Linear(16, 4))) // one logit per SimulationAction
            .to(_device);
    }

    public string Name => "MLP Policy";

    public Module<Tensor, Tensor> Network { get; }

    // Weights persist across runs deliberately -- resetting on every run would
    // undo whatever training has accumulated, defeating "trains by trading."
    public void Reset() { }

    public Tensor Encode(SimulationObservation observation)
    {
        double currentPrice = observation.CurrentPrice;
        double[] prices = observation.RecentBars.Count > 0
            ? observation.RecentBars.Select(b => b.Close).ToArray()
            : observation.RecentTicks.Select(t => (t.Bid + t.Ask) / 2.0).ToArray();

        var features = new float[FeatureCount];

        // Percent-change-from-current-price for the last WindowSize prices, most-
        // recent-last; zero-padded at the front early in an episode (neutral, no
        // false signal) when fewer than WindowSize observations exist yet.
        int available = Math.Min(WindowSize, prices.Length);
        int padding = WindowSize - available;
        for (int i = 0; i < available; i++)
        {
            double price = prices[prices.Length - available + i];
            features[padding + i] = currentPrice == 0 ? 0f : (float)((price - currentPrice) / currentPrice);
        }

        // Bounded capital/position ratios -- no dependency on knowing starting cash.
        var capital = observation.Capital;
        double equity = capital.Equity;
        features[WindowSize + 0] = observation.Position is { } position
            ? (position.Direction == PositionDirection.Long ? 1f : -1f)
            : 0f;
        features[WindowSize + 1] = equity != 0 ? (float)(capital.UnrealizedPnl / equity) : 0f;
        features[WindowSize + 2] = equity != 0 ? (float)(capital.RealizedPnl / equity) : 0f;
        features[WindowSize + 3] = equity != 0 ? (float)(capital.Cash / equity) : 0f;

        return tensor(features).unsqueeze(0).to(_device);
    }

    public IReadOnlyList<RlInputFeature> DescribeInputs(SimulationObservation observation)
    {
        lock (_forwardLock)
        {
            using var scope = NewDisposeScope();
            using var noGradGuard = no_grad();
            using var input = Encode(observation);

            var features = new List<RlInputFeature>(FeatureCount);
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
