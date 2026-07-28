// ── Encoder: trailing bar/tick price window ──────────────────────────────
//  Extracted from the formerly-duplicated MlpPolicyBrain/LstmPolicyBrain
//  Encode logic (identical in both) -- the last WindowSize prices, most-
//  recent-last, zero-padded at the front (neutral, no false signal) when
//  fewer than WindowSize observations exist yet -- a fallback for live
//  Simulation playback, which really can start with no history.
//  RlEpisodeRunner's WarmupBars-driven warm-up phase (see
//  IObservationEncoder.WarmupBars) means training never exercises this
//  padding path: a training episode's first decision only happens once
//  WindowSize real bars/ticks have already accumulated. Reads straight off
//  the source buffer (up to 5000 ticks) instead of copying the whole thing
//  via LINQ just to keep the trailing WindowSize.
//
//  Normalization picks the feature convention: Normalized (default) feeds
//  percent-change-from-current-price via PriceOffsetNormalizer; Raw feeds
//  the literal price via RawPriceNormalizer instead. Same "t-N" labels
//  either way -- the two modes are alternatives, never combined in one
//  brain, so there's no collision to avoid by splitting the prefix.
//
//  SequenceLength == FeatureCount (the whole output is one contiguous
//  sequence) -- this is the one encoder in this codebase meant to be listed
//  first when composed via CompositeEncoder, so a recurrent architecture
//  (see LstmArchitecture) can narrow() it out as a true time series.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class BarWindowEncoder : IObservationEncoder
{
    private const int DefaultWindowSize = 20;

    private readonly int _windowSize;
    private readonly IFeatureNormalizer _normalizer;
    private readonly IReadOnlyList<FeatureDescriptor> _features;

    public BarWindowEncoder(
        int windowSize = DefaultWindowSize, NormalizationKind normalization = NormalizationKind.Normalized, IFeatureNormalizer? normalizer = null)
    {
        _windowSize = windowSize;
        _normalizer = normalizer ?? (normalization == NormalizationKind.Raw ? new RawPriceNormalizer() : new PriceOffsetNormalizer());
        _features = Enumerable.Range(1, _windowSize)
            .Select(i => new FeatureDescriptor($"t-{_windowSize - i + 1}", _normalizer))
            .ToList();
    }

    public int FeatureCount => _windowSize;

    public int SequenceLength => _windowSize;

    public int WarmupBars => _windowSize;

    public IReadOnlyList<FeatureDescriptor> Features => _features;

    public float[] Encode(SimulationObservation observation)
    {
        bool useBars = observation.RecentBars.Count > 0;
        int sourceCount = useBars ? observation.RecentBars.Count : observation.RecentTicks.Count;

        var features = new float[_windowSize];

        int available = Math.Min(_windowSize, sourceCount);
        int padding = _windowSize - available;
        for (int i = 0; i < available; i++)
        {
            int sourceIndex = sourceCount - available + i;
            double price = useBars
                ? observation.RecentBars[sourceIndex].Close
                : (observation.RecentTicks[sourceIndex].Bid + observation.RecentTicks[sourceIndex].Ask) / 2.0;
            features[padding + i] = _normalizer.Normalize(price, observation);
        }

        return features;
    }
}
