// ── Encoder: trailing bar/tick price window ──────────────────────────────
//  Extracted from the formerly-duplicated MlpPolicyBrain/LstmPolicyBrain
//  Encode logic (identical in both) -- the last WindowSize prices as
//  percent-change-from-current-price, most-recent-last, zero-padded at the
//  front (neutral, no false signal) when fewer than WindowSize observations
//  exist yet -- a fallback for live Simulation playback, which really can
//  start with no history. RlEpisodeRunner's WarmupBars-driven warm-up phase
//  (see IObservationEncoder.WarmupBars) means training never exercises this
//  padding path: a training episode's first decision only happens once
//  WindowSize real bars/ticks have already accumulated. Reads straight off
//  the source buffer (up to 5000 ticks) instead of copying the whole thing
//  via LINQ just to keep the trailing WindowSize.
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
    private const int WindowSize = 20;

    private readonly IFeatureNormalizer _priceOffsetNormalizer;
    private readonly IReadOnlyList<FeatureDescriptor> _features;

    public BarWindowEncoder(IFeatureNormalizer? priceOffsetNormalizer = null)
    {
        _priceOffsetNormalizer = priceOffsetNormalizer ?? new PriceOffsetNormalizer();
        _features = Enumerable.Range(1, WindowSize)
            .Select(i => new FeatureDescriptor($"t-{WindowSize - i + 1}", _priceOffsetNormalizer))
            .ToList();
    }

    public int FeatureCount => WindowSize;

    public int SequenceLength => WindowSize;

    public int WarmupBars => WindowSize;

    public IReadOnlyList<FeatureDescriptor> Features => _features;

    public float[] Encode(SimulationObservation observation)
    {
        bool useBars = observation.RecentBars.Count > 0;
        int sourceCount = useBars ? observation.RecentBars.Count : observation.RecentTicks.Count;

        var features = new float[WindowSize];

        int available = Math.Min(WindowSize, sourceCount);
        int padding = WindowSize - available;
        for (int i = 0; i < available; i++)
        {
            int sourceIndex = sourceCount - available + i;
            double price = useBars
                ? observation.RecentBars[sourceIndex].Close
                : (observation.RecentTicks[sourceIndex].Bid + observation.RecentTicks[sourceIndex].Ask) / 2.0;
            features[padding + i] = _priceOffsetNormalizer.Normalize(price, observation);
        }

        return features;
    }
}
