using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Helpers;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class TimeProfileEncoder : IObservationEncoder
{
	// Matches VolumeProfileEncoder's bucket count.
	private const int DefaultBucketCount = 20;
	private const int DefaultLookbackBars = 50;

	// POC, value-area-high, value-area-low.
	private const int SummaryScalarCount = 3;
	private const double ValueAreaFraction = 0.70;

	private readonly int _bucketCount;

	// Slots/bucket: 1 (Normalized), 2 (Raw), 3 (Normalized+SplitUpDown), or 4 (Raw+SplitUpDown).
	private readonly int _bucketFeatureCount;
	private readonly int _lookbackBars;
	private readonly NormalizationKind _normalization;
	private readonly bool _splitUpDown;
	private readonly IFeatureNormalizer _bucketNormalizer;
	private readonly IFeatureNormalizer _priceNormalizer;
	private readonly IReadOnlyList<FeatureDescriptor> _features;

	public TimeProfileEncoder(
		int bucketCount = DefaultBucketCount,
		int lookbackBars = DefaultLookbackBars,
		NormalizationKind normalization = NormalizationKind.Normalized,
		bool splitUpDown = false,
		IFeatureNormalizer? bucketNormalizer = null,
		IFeatureNormalizer? priceNormalizer = null
	)
	{
		_bucketCount = bucketCount;
		_lookbackBars = lookbackBars;
		_normalization = normalization;
		_splitUpDown = splitUpDown;
		_bucketNormalizer =
			bucketNormalizer
			?? (normalization == NormalizationKind.Raw ? new RawValueNormalizer() : new FractionNormalizer());
		_priceNormalizer =
			priceNormalizer
			?? (normalization == NormalizationKind.Raw ? new RawPriceNormalizer() : new PriceOffsetNormalizer());

		if (normalization == NormalizationKind.Raw)
		{
			_bucketFeatureCount = bucketCount * (splitUpDown ? 4 : 2);
			_features = Enumerable
				.Range(0, _bucketCount)
				.SelectMany(i =>
					splitUpDown
						? (FeatureDescriptor[])
							[
								new($"tpr-price-{i}", _priceNormalizer),
								new($"tpr-up-{i}", _bucketNormalizer),
								new($"tpr-down-{i}", _bucketNormalizer),
								new($"tpr-diff-{i}", _bucketNormalizer),
							]
						: [new($"tpr-price-{i}", _priceNormalizer), new($"tpr-sec-{i}", _bucketNormalizer)]
				)
				.Append(new FeatureDescriptor("tpr-poc", _priceNormalizer))
				.Append(new FeatureDescriptor("tpr-va-high", _priceNormalizer))
				.Append(new FeatureDescriptor("tpr-va-low", _priceNormalizer))
				.ToList();
		}
		else
		{
			_bucketFeatureCount = bucketCount * (splitUpDown ? 3 : 1);
			_features = Enumerable
				.Range(0, _bucketCount)
				.SelectMany(i =>
					splitUpDown
						? (FeatureDescriptor[])
							[
								new($"tp-up-{i}", _bucketNormalizer),
								new($"tp-down-{i}", _bucketNormalizer),
								new($"tp-diff-{i}", _bucketNormalizer),
							]
						: [new($"tp-{i}", _bucketNormalizer)]
				)
				.Append(new FeatureDescriptor("tp-poc-offset", _priceNormalizer))
				.Append(new FeatureDescriptor("tp-va-high-offset", _priceNormalizer))
				.Append(new FeatureDescriptor("tp-va-low-offset", _priceNormalizer))
				.ToList();
		}
	}

	public int FeatureCount => _bucketFeatureCount + SummaryScalarCount;

	public int SequenceLength => 0;

	// Same number as LookbackBars -- this encoder's own trailing-merge
	// window IS its warm-up requirement (see IObservationEncoder.WarmupBars).
	public int WarmupBars => _lookbackBars;

	public IReadOnlyList<FeatureDescriptor> Features => _features;

	public float[] Encode(SimulationObservation observation)
	{
		var merged = BarPriceHistogram.MergeTime(
			observation.RecentBars,
			observation.RecentBarHistograms,
			_lookbackBars
		);
		double bucketWidth =
			observation.RecentBarHistograms.Count > 0 ? observation.RecentBarHistograms[^1].BucketWidth : 0;

		var features = new float[FeatureCount];

		if (_normalization == NormalizationKind.Raw && _splitUpDown)
		{
			var (up, down, diff, prices) = TimeProfileHelper.BuildRawSplitHistogram(merged, _bucketCount);
			for (int i = 0; i < _bucketCount; i++)
			{
				features[i * 4] = _priceNormalizer.Normalize(prices[i], observation);
				features[i * 4 + 1] = _bucketNormalizer.Normalize(up[i], observation);
				features[i * 4 + 2] = _bucketNormalizer.Normalize(down[i], observation);
				features[i * 4 + 3] = _bucketNormalizer.Normalize(diff[i], observation);
			}
		}
		else if (_normalization == NormalizationKind.Raw)
		{
			var (seconds, prices) = TimeProfileHelper.BuildRawHistogram(merged, _bucketCount);
			for (int i = 0; i < _bucketCount; i++)
			{
				features[i * 2] = _priceNormalizer.Normalize(prices[i], observation);
				features[i * 2 + 1] = _bucketNormalizer.Normalize(seconds[i], observation);
			}
		}
		else if (_splitUpDown)
		{
			var (up, down, diff) = TimeProfileHelper.BuildRelativeSplitHistogram(merged, _bucketCount);
			for (int i = 0; i < _bucketCount; i++)
			{
				features[i * 3] = _bucketNormalizer.Normalize(up[i], observation);
				features[i * 3 + 1] = _bucketNormalizer.Normalize(down[i], observation);
				features[i * 3 + 2] = _bucketNormalizer.Normalize(diff[i], observation);
			}
		}
		else
		{
			var histogram = TimeProfileHelper.BuildRelativeHistogram(merged, _bucketCount);
			for (int i = 0; i < _bucketCount; i++)
				features[i] = _bucketNormalizer.Normalize(histogram[i], observation);
		}

		if (TimeProfileHelper.ComputeSummary(merged, bucketWidth, ValueAreaFraction) is { } summary)
		{
			features[_bucketFeatureCount + 0] = _priceNormalizer.Normalize(summary.PointOfControl, observation);
			features[_bucketFeatureCount + 1] = _priceNormalizer.Normalize(summary.ValueAreaHigh, observation);
			features[_bucketFeatureCount + 2] = _priceNormalizer.Normalize(summary.ValueAreaLow, observation);
		}

		return features;
	}
}
