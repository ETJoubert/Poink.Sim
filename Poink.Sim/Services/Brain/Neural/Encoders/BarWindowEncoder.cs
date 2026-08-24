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
		int windowSize = DefaultWindowSize,
		NormalizationKind normalization = NormalizationKind.Normalized,
		int zScoreSubWindowSize = DefaultZScoreSubWindowSize,
		IFeatureNormalizer? normalizer = null
	)
	{
		_windowSize = windowSize;
		_normalization = normalization;
		_zScoreSubWindowSize = zScoreSubWindowSize;
		_normalizer =
			normalizer
			?? (
				normalization switch
				{
					NormalizationKind.Raw => new RawPriceNormalizer(),
					NormalizationKind.LocalZScore or NormalizationKind.GlobalZScore => new ZScoreNormalizer(),
					NormalizationKind.Binary => new BinaryNormalizer(),
					_ => new PriceOffsetNormalizer(),
				}
			);
		_features = Enumerable
			.Range(1, _windowSize)
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
			var z =
				_normalization == NormalizationKind.GlobalZScore
					? ZScoreNormalization.Global(prices)
					: ZScoreNormalization.Local(prices, _zScoreSubWindowSize);
			for (int i = 0; i < available; i++)
				features[padding + i] = _normalizer.Normalize(z[i], observation);
		}
		else if (_normalization == NormalizationKind.Binary)
		{
			var b = BinaryNormalization.Compute(prices);
			for (int i = 0; i < available; i++)
				features[padding + i] = _normalizer.Normalize(b[i], observation);
		}
		else
		{
			for (int i = 0; i < available; i++)
				features[padding + i] = _normalizer.Normalize(prices[i], observation);
		}

		return features;
	}
}
