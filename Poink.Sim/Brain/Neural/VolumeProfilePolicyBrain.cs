// ── Second concrete policy-gradient brain: volume profile instead of a raw
//  price window ───────────────────────────────────────────────────────────
//  Same MLP shape family as MlpPolicyBrain (this is a controlled experiment
//  on the *input*, not a novel architecture) but the market-data half of the
//  input is replaced entirely: instead of the last WindowSize prices, this
//  brain merges its own trailing LookbackBars completed bars' price
//  histograms (see BarPriceHistogram.Merge) into a fixed-width relative
//  histogram plus point-of-control/value-area summary scalars (see
//  VolumeProfileAnalysis). LookbackBars is settable (IHasLookbackBars) --
//  RlTrainingService assigns it once at training start from the Train
//  page's form so different window sizes can be evaluated, while the
//  network's input width stays fixed (HistogramBucketCount never changes).
//
//  The 4 capital/position scalars are unchanged from MlpPolicyBrain -- the
//  agent still needs to know whether it already holds a position regardless
//  of how the market data itself is represented.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;
using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Poink.Sim.Brain.Neural;

internal sealed class VolumeProfilePolicyBrain : IPolicyGradientBrain, IHasLookbackBars
{
    private const int HistogramBucketCount = 20; // matches TickVolumeProfileChart's default bucket count
    private const int SummaryScalarCount = 3; // POC offset, value-area-high offset, value-area-low offset
    private const int ScalarFeatureCount = 4; // position direction, unrealized/equity, realized/equity, cash/equity
    private const int FeatureCount = HistogramBucketCount + SummaryScalarCount + ScalarFeatureCount;
    private const double CapitalFraction = 0.5;
    private const double ValueAreaFraction = 0.70;

    private readonly object _forwardLock = new();

    public VolumeProfilePolicyBrain()
    {
        Network = Sequential(
            ("hidden1", Linear(FeatureCount, 32)),
            ("fold1", Tanh()),
            ("hidden2", Linear(32, 16)),
            ("fold2", Tanh()),
            ("heads", Linear(16, 4))); // one logit per SimulationAction
    }

    public string Name => "Volume Profile Policy";

    // How many trailing completed bars' histograms to merge into one profile
    // each decision -- set once per training run (see IHasLookbackBars's doc
    // comment); live Simulation playback uses whatever was last configured.
    public int LookbackBars { get; set; } = 50;

    public Module<Tensor, Tensor> Network { get; }

    private Device CurrentDevice => Network.parameters().First().device;

    public void Reset() { }

    public Tensor Encode(SimulationObservation observation)
    {
        double currentPrice = observation.CurrentPrice;
        var merged = BarPriceHistogram.Merge(observation.RecentBarHistograms, LookbackBars);
        double bucketWidth = observation.RecentBarHistograms.Count > 0
            ? observation.RecentBarHistograms[^1].BucketWidth
            : 0;

        var features = new float[FeatureCount];

        var histogram = VolumeProfileAnalysis.BuildRelativeHistogram(merged, HistogramBucketCount);
        Array.Copy(histogram, features, HistogramBucketCount);

        var summary = VolumeProfileAnalysis.ComputeSummary(merged, bucketWidth, currentPrice, ValueAreaFraction);
        features[HistogramBucketCount + 0] = (float)summary.PointOfControlOffset;
        features[HistogramBucketCount + 1] = (float)summary.ValueAreaHighOffset;
        features[HistogramBucketCount + 2] = (float)summary.ValueAreaLowOffset;

        // Bounded capital/position ratios -- identical to MlpPolicyBrain's, no
        // dependency on knowing starting cash.
        var capital = observation.Capital;
        double equity = capital.Equity;
        int scalarBase = HistogramBucketCount + SummaryScalarCount;
        features[scalarBase + 0] = observation.Position is { } position
            ? (position.Direction == PositionDirection.Long ? 1f : -1f)
            : 0f;
        features[scalarBase + 1] = equity != 0 ? (float)(capital.UnrealizedPnl / equity) : 0f;
        features[scalarBase + 2] = equity != 0 ? (float)(capital.RealizedPnl / equity) : 0f;
        features[scalarBase + 3] = equity != 0 ? (float)(capital.Cash / equity) : 0f;

        return tensor(features).unsqueeze(0).to(CurrentDevice);
    }

    public IReadOnlyList<RlInputFeature> DescribeInputs(SimulationObservation observation)
    {
        lock (_forwardLock)
        {
            using var scope = NewDisposeScope();
            using var noGradGuard = no_grad();
            using var input = Encode(observation);

            var features = new List<RlInputFeature>(FeatureCount);
            for (int i = 0; i < HistogramBucketCount; i++)
                features.Add(new RlInputFeature($"vp-{i}", input[0, i].item<float>()));
            features.Add(new RlInputFeature("poc-offset", input[0, HistogramBucketCount + 0].item<float>()));
            features.Add(new RlInputFeature("va-high-offset", input[0, HistogramBucketCount + 1].item<float>()));
            features.Add(new RlInputFeature("va-low-offset", input[0, HistogramBucketCount + 2].item<float>()));
            int scalarBase = HistogramBucketCount + SummaryScalarCount;
            features.Add(new RlInputFeature("position", input[0, scalarBase + 0].item<float>()));
            features.Add(new RlInputFeature("unrealized/eq", input[0, scalarBase + 1].item<float>()));
            features.Add(new RlInputFeature("realized/eq", input[0, scalarBase + 2].item<float>()));
            features.Add(new RlInputFeature("cash/eq", input[0, scalarBase + 3].item<float>()));
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

    // See MlpPolicyBrain.SampleActionIndex's doc comment -- same native-crash
    // hazard (native multinomial), same guard, same manual-sampling fix.
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
