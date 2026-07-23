// ── Encoder: trailing bar/tick price window ──────────────────────────────
//  Extracted from the formerly-duplicated MlpPolicyBrain/LstmPolicyBrain
//  Encode logic (identical in both) -- the last WindowSize prices as
//  percent-change-from-current-price, most-recent-last, zero-padded at the
//  front early in an episode (neutral, no false signal) when fewer than
//  WindowSize observations exist yet. Reads straight off the source buffer
//  (up to 5000 ticks) instead of copying the whole thing via LINQ just to
//  keep the trailing WindowSize.
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

    private static readonly IReadOnlyList<string> Labels =
        Enumerable.Range(1, WindowSize).Select(i => $"t-{WindowSize - i + 1}").ToList();

    public int FeatureCount => WindowSize;

    public int SequenceLength => WindowSize;

    public IReadOnlyList<string> FeatureLabels => Labels;

    public float[] Encode(SimulationObservation observation)
    {
        double currentPrice = observation.CurrentPrice;
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
            features[padding + i] = currentPrice == 0 ? 0f : (float)((price - currentPrice) / currentPrice);
        }

        return features;
    }
}
