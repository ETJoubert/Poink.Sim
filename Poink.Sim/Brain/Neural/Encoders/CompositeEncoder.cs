// ── Concatenates N encoders into one ─────────────────────────────────────
//  How multiple IObservationEncoder parts combine into the one flat feature
//  vector a ComposedPolicyBrain's Network actually sees -- FeatureCount/
//  SequenceLength are sums, FeatureLabels/Encode are straight concatenations
//  in the order the caller lists parts.
//
//  Ordering constraint: if any part is sequence-bearing (SequenceLength > 0,
//  e.g. BarWindowEncoder), it must be parts[0] -- a recurrent architecture
//  (see LstmArchitecture) narrow()s the LEADING slice of the flat vector as
//  its sequence, so a sequence-bearing part anywhere else would silently
//  feed the wrong slice through the LSTM instead of failing loudly. Create
//  enforces this at construction time rather than leaving it as a documented
//  assumption someone can violate by accident.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class CompositeEncoder : IObservationEncoder
{
    private readonly IReadOnlyList<IObservationEncoder> _parts;

    private CompositeEncoder(IReadOnlyList<IObservationEncoder> parts)
    {
        _parts = parts;
        FeatureCount = parts.Sum(p => p.FeatureCount);
        SequenceLength = parts.Sum(p => p.SequenceLength);
        FeatureLabels = parts.SelectMany(p => p.FeatureLabels).ToList();
    }

    public static CompositeEncoder Create(params IObservationEncoder[] parts)
    {
        if (parts.Length == 0)
            throw new InvalidOperationException("CompositeEncoder needs at least one part");

        for (int i = 1; i < parts.Length; i++)
            if (parts[i].SequenceLength > 0)
                throw new InvalidOperationException(
                    $"{parts[i].GetType().Name} is sequence-bearing (SequenceLength={parts[i].SequenceLength}) but isn't listed " +
                    "first among CompositeEncoder's parts -- a recurrent architecture's leading-slice convention requires it first");

        return new CompositeEncoder(parts);
    }

    public int FeatureCount { get; }

    public int SequenceLength { get; }

    // Max, not sum -- every part reads from the same shared recentBars/
    // recentBarHistograms tail independently (not disjoint slices), so once
    // the deepest part's requirement is met, every shallower part's is too.
    // Computed live rather than cached alongside FeatureCount/SequenceLength
    // above: unlike those, a part's WarmupBars can change after construction
    // (VolumeProfileEncoder.LookbackBars is mutable -- RlTrainingService sets
    // it at each training run's start, see IHasLookbackBars).
    public int WarmupBars => _parts.Count == 0 ? 0 : _parts.Max(p => p.WarmupBars);

    public IReadOnlyList<string> FeatureLabels { get; }

    public float[] Encode(SimulationObservation observation)
    {
        var features = new float[FeatureCount];
        int offset = 0;
        foreach (var part in _parts)
        {
            var partFeatures = part.Encode(observation);
            Array.Copy(partFeatures, 0, features, offset, partFeatures.Length);
            offset += partFeatures.Length;
        }
        return features;
    }
}
