using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class XGridSignalEncoder : IObservationEncoder
{
	private const int DefaultRowPips = 20;
	private const double DefaultPipSize = 0.01;
	private const int DefaultColCount = 20;
	private const int DefaultWindowBars = 60;
	private const double DefaultPercent = 10;

	private readonly IReadOnlyList<string> _signals;
	private readonly double _percent;
	private readonly int _rowPips;
	private readonly double _pipSize;
	private readonly int _colCount;
	private readonly int _windowBars;
	private readonly IFeatureNormalizer _binaryNormalizer;
	private readonly IReadOnlyList<FeatureDescriptor> _features;

	public XGridSignalEncoder(
		IReadOnlyList<string> signals,
		double percent = DefaultPercent,
		int rowPips = DefaultRowPips,
		double pipSize = DefaultPipSize,
		int colCount = DefaultColCount,
		int windowBars = DefaultWindowBars,
		IFeatureNormalizer? binaryNormalizer = null
	)
	{
		_signals = signals;
		_percent = percent;
		_rowPips = Math.Max(1, rowPips);
		_pipSize = pipSize > 0 ? pipSize : DefaultPipSize;
		_colCount = Math.Max(1, colCount);
		_windowBars = Math.Max(1, windowBars);
		_binaryNormalizer = binaryNormalizer ?? new BinaryNormalizer();
		_features = _signals.Select(s => new FeatureDescriptor($"xgrid-{s}", _binaryNormalizer)).ToList();
	}

	public int FeatureCount => _signals.Count;

	public int SequenceLength => 0;

	public int WarmupBars => _windowBars;

	public IReadOnlyList<FeatureDescriptor> Features => _features;

	// Evaluated against this encoder's own grid/threshold config regardless of which signals
	// it was configured to expose as input features, so a mask rule can reuse it as-is.
	public bool HasSignal(string kind, SimulationObservation observation) =>
		BuildGrid(observation) is { } grid && XGridSignalCatalog.Value(grid, kind, _percent);

	public float[] Encode(SimulationObservation observation)
	{
		var features = new float[FeatureCount];
		if (_signals.Count == 0)
			return features;

		if (BuildGrid(observation) is not { } grid)
			return features;

		for (int i = 0; i < _signals.Count; i++)
		{
			bool value = XGridSignalCatalog.Value(grid, _signals[i], _percent);
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

		return XGridStats.Compute(
			bars,
			histograms,
			samples,
			bars[0].OpenTime,
			bars[^1].OpenTime,
			_rowPips * _pipSize,
			_colCount
		);
	}
}
