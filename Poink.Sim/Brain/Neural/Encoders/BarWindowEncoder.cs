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
//  the literal price via RawPriceNormalizer instead; LocalZScore/GlobalZScore
//  feed each price's z-score (see ZScoreNormalization) against its own
//  sub-window's (Local) or the whole trailing window's (Global) mean/stddev,
//  computed over the window's real prices only -- zero-padding entries are
//  excluded from the mean/stddev and left at their neutral 0. Binary feeds
//  1 if a price is higher than the price immediately before it, 0 if lower
//  or equal (see BinaryNormalization) -- the window's first real price has
//  no in-window predecessor, so it lands on the same neutral 0 as a
//  zero-padding entry. Same "t-N" labels regardless of mode -- these are
//  alternatives, never combined in one brain, so there's no collision to
//  avoid by splitting the prefix.
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

    private const int DefaultZScoreSubWindowSize = 5;

    private readonly int _windowSize;
    private readonly NormalizationKind _normalization;
    private readonly int _zScoreSubWindowSize;
    private readonly IFeatureNormalizer _normalizer;
    private readonly IReadOnlyList<FeatureDescriptor> _features;

    public BarWindowEncoder(
        int windowSize = DefaultWindowSize, NormalizationKind normalization = NormalizationKind.Normalized,
        int zScoreSubWindowSize = DefaultZScoreSubWindowSize, IFeatureNormalizer? normalizer = null)
    {
        _windowSize = windowSize;
        _normalization = normalization;
        _zScoreSubWindowSize = zScoreSubWindowSize;
        _normalizer = normalizer ?? (normalization switch
        {
            NormalizationKind.Raw => new RawPriceNormalizer(),
            NormalizationKind.LocalZScore or NormalizationKind.GlobalZScore => new ZScoreNormalizer(),
            NormalizationKind.Binary => new BinaryNormalizer(),
            _ => new PriceOffsetNormalizer(),
        });
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

        var prices = new double[available];
        for (int i = 0; i < available; i++)
        {
            int sourceIndex = sourceCount - available + i;
            prices[i] = useBars
                ? observation.RecentBars[sourceIndex].Close
                : (observation.RecentTicks[sourceIndex].Bid + observation.RecentTicks[sourceIndex].Ask) / 2.0;
        }

        if (_normalization is NormalizationKind.LocalZScore or NormalizationKind.GlobalZScore)
        {
            var z = _normalization == NormalizationKind.GlobalZScore
                ? ZScoreNormalization.Global(prices)
                : ZScoreNormalization.Local(prices, _zScoreSubWindowSize);
            for (int i = 0; i < available; i++) features[padding + i] = _normalizer.Normalize(z[i], observation);
        }
        else if (_normalization == NormalizationKind.Binary)
        {
            var b = BinaryNormalization.Compute(prices);
            for (int i = 0; i < available; i++) features[padding + i] = _normalizer.Normalize(b[i], observation);
        }
        else
        {
            for (int i = 0; i < available; i++) features[padding + i] = _normalizer.Normalize(prices[i], observation);
        }

        return features;
    }
}
