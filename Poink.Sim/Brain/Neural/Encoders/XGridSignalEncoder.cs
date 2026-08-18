
using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class XGridSignalEncoder : IObservationEncoder
{
    private const int DefaultRowCount = 20;
    private const int DefaultColCount = 20;
    private const int DefaultWindowBars = 60;
    private const double DefaultPercent = 10;

    private readonly IReadOnlyList<XGridSignalKind> _signals;
    private readonly double _percent;
    private readonly int _rowCount;
    private readonly int _colCount;
    private readonly int _windowBars;
    private readonly IFeatureNormalizer _binaryNormalizer;
    private readonly IReadOnlyList<FeatureDescriptor> _features;

    public XGridSignalEncoder(
        IReadOnlyList<XGridSignalKind> signals, double percent = DefaultPercent,
        int rowCount = DefaultRowCount, int colCount = DefaultColCount, int windowBars = DefaultWindowBars,
        IFeatureNormalizer? binaryNormalizer = null)
    {
        _signals = signals;
        _percent = percent;
        _rowCount = Math.Max(1, rowCount);
        _colCount = Math.Max(1, colCount);
        _windowBars = Math.Max(1, windowBars);
        _binaryNormalizer = binaryNormalizer ?? new BinaryNormalizer();
        _features = _signals.Select(s => new FeatureDescriptor($"xgrid-{XGridSignals.Label(s)}", _binaryNormalizer)).ToList();
    }

    public int FeatureCount => _signals.Count;

    public int SequenceLength => 0;

    public int WarmupBars => _windowBars;

    public IReadOnlyList<FeatureDescriptor> Features => _features;

    public float[] Encode(SimulationObservation observation)
    {
        var features = new float[FeatureCount];
        if (_signals.Count == 0)
            return features;

        if (BuildGrid(observation) is not { } grid)
            return features;

        for (int i = 0; i < _signals.Count; i++)
        {
            bool value = XGridSignals.Value(grid, _signals[i], _percent);
            features[i] = _binaryNormalizer.Normalize(value ? 1 : 0, observation);
        }
        return features;
    }

    // RecentBars/RecentBarHistograms are appended in lockstep everywhere they're produced
    // (SimulationEngine, RlEpisodeRunner) -- same length, same index order.
    private XGridResult? BuildGrid(SimulationObservation observation)
    {
        var allBars = observation.RecentBars;
        if (allBars.Count == 0)
            return null;

        int start = Math.Max(0, allBars.Count - _windowBars);
        var bars = allBars.Skip(start).ToList();
        var histograms = observation.RecentBarHistograms.Skip(start).ToList();

        var merged = BarPriceHistogram.Merge(histograms, histograms.Count);
        var samples = merged.Select(t => new WeightedPriceSample(t.Price, t.Count, t.UpCount, t.DownCount)).ToList();

        return XGridStats.Compute(bars, histograms, samples, bars[0].OpenTime, bars[^1].OpenTime, _rowCount, _colCount);
    }
}
