// ── v1 IActionOutputHead: sample one of a small fixed set of levels ─────
//  Each enabled ActionOutputKind gets its own small independent categorical
//  head (levels below), picked via CategoricalSampling -- the same manual
//  cumulative-sum sampler ComposedPolicyBrain's direction pick uses, reused
//  here rather than duplicated. Level tables mirror RandomBrain's existing
//  fixed stop-loss/take-profit percentages (0.003/0.006) as their "Medium"
//  entries.
// ─────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Poink.Sim.Brain.Neural;

internal sealed class DiscretizedLevelsActionHead : IActionOutputHead
{
    // Value: real units already (capital fraction 0..1 for Volume, percent-
    // distance-from-price for StopLoss/TakeProfit); null = "don't set it"
    // -- only meaningful for StopLoss/TakeProfit's "None" level.
    private static readonly IReadOnlyDictionary<ActionOutputKind, (string Label, double? Value)[]> LevelsByKind =
        new Dictionary<ActionOutputKind, (string, double?)[]>
        {
            [ActionOutputKind.Volume] = [("25%", 0.25), ("50%", 0.5), ("75%", 0.75), ("100%", 1.0)],
            [ActionOutputKind.StopLoss] = [("None", null), ("Tight", 0.0015), ("Medium", 0.003), ("Wide", 0.006)],
            [ActionOutputKind.TakeProfit] = [("None", null), ("Tight", 0.003), ("Medium", 0.006), ("Wide", 0.012)],
        };

    private readonly (string Label, double? Value)[] _levels;
    private readonly DecisionDecodingKind _decoding;

    public DiscretizedLevelsActionHead(ActionOutputKind kind, DecisionDecodingKind decoding = DecisionDecodingKind.Softmax)
    {
        Kind = kind;
        _levels = LevelsByKind[kind];
        _decoding = decoding;
    }

    public ActionOutputKind Kind { get; }

    public int OutputWidth => _levels.Length;

    public (double? Value, Tensor LogProbability, IReadOnlyList<RlOutputFeature> Outputs) Sample(Tensor rawSlice)
    {
        var logProbs = functional.log_softmax(rawSlice, dim: 1);
        var probabilities = exp(logProbs);
        int levelIndex = _decoding == DecisionDecodingKind.HardMax
            ? CategoricalSampling.ArgmaxIndex(probabilities)
            : CategoricalSampling.SampleIndex(probabilities);

        var outputs = new List<RlOutputFeature>(_levels.Length);
        for (int i = 0; i < _levels.Length; i++)
            outputs.Add(new RlOutputFeature(
                _levels[i].Label, probabilities[0, i].item<float>(), i == levelIndex,
                rawSlice[0, i].item<float>(), Group: Kind.ToString()));

        return (_levels[levelIndex].Value, logProbs[0, levelIndex], outputs);
    }
}
