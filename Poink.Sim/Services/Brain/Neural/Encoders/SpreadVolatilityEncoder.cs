
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

        _normalizer = normalizer ?? new RawPriceNormalizer();
        _features =
        [
            new FeatureDescriptor("spread", _normalizer),
            new FeatureDescriptor("atr", _normalizer),
        ];
    }

    public int FeatureCount => Count;

    public int SequenceLength => 0;

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
