// ── Encoder: current spread + short-window volatility ───────────────────
//  Two cheap, per-decision market-condition scalars neither BarWindowEncoder
//  nor either profile encoder surfaces on their own: the live Bid/Ask spread
//  (execution cost right now -- widens sharply around session opens/
//  rollover/news, exactly when a policy most needs to know trading is
//  expensive) and a short trailing Average True Range (the "how much is
//  price actually moving" volatility figure a network would otherwise have
//  to infer indirectly from BarWindowEncoder's raw price sequence).
//
//  ATR uses the standard true-range definition -- High-Low, or the gap
//  against the previous bar's Close if that's wider (gaps matter in FX
//  around session opens, unlike a plain High-Low range) -- averaged over
//  AtrPeriod trailing bars; needs AtrPeriod+1 bars of history (one extra for
//  the oldest window bar's own previous close). Bar-only, like
//  VolumeProfileEncoder/TimeProfileEncoder: reads RecentBars, and leaves ATR
//  at the array-initialized 0f (same "no false signal" convention those two
//  use for an empty/undersized window) until enough bars have accumulated --
//  raw-tick mode (aggregate: false) never populates RecentBars, so ATR stays
//  neutral there. Spread has no such warm-up: Bid/Ask are this decision's own
//  tick, valid from the first one.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class SpreadVolatilityEncoder : IObservationEncoder
{
    private const int Count = 2;
    private const int DefaultAtrPeriod = 14;

    private readonly int _atrPeriod;
    private readonly IFeatureNormalizer _normalizer;
    private readonly IReadOnlyList<FeatureDescriptor> _features;

    public SpreadVolatilityEncoder(int atrPeriod = DefaultAtrPeriod, IFeatureNormalizer? normalizer = null)
    {
        _atrPeriod = atrPeriod;
        // Both are literal price-scaled quantities (pip-sized deltas/ranges),
        // same convention TraderStateEncoder's current-price feature uses --
        // RawPriceNormalizer's F5 formatting keeps FX-quote precision.
        _normalizer = normalizer ?? new RawPriceNormalizer();
        _features =
        [
            new FeatureDescriptor("spread", _normalizer),
            new FeatureDescriptor("atr", _normalizer),
        ];
    }

    public int FeatureCount => Count;

    public int SequenceLength => 0;

    // One extra bar beyond AtrPeriod -- the oldest bar in the averaging
    // window still needs its own previous Close to compute a true range.
    public int WarmupBars => _atrPeriod + 1;

    public IReadOnlyList<FeatureDescriptor> Features => _features;

    public float[] Encode(SimulationObservation observation)
    {
        var features = new float[Count];
        features[0] = _normalizer.Normalize(observation.Ask - observation.Bid, observation);

        var bars = observation.RecentBars;
        if (bars.Count >= _atrPeriod + 1)
        {
            double trueRangeSum = 0;
            for (int i = bars.Count - _atrPeriod; i < bars.Count; i++)
            {
                double previousClose = bars[i - 1].Close;
                trueRangeSum += Math.Max(
                    bars[i].High - bars[i].Low,
                    Math.Max(Math.Abs(bars[i].High - previousClose), Math.Abs(bars[i].Low - previousClose)));
            }
            features[1] = _normalizer.Normalize(trueRangeSum / _atrPeriod, observation);
        }

        return features;
    }
}
