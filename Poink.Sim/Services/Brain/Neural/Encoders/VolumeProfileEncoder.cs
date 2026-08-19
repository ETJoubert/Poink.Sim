
using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class VolumeProfileEncoder : IObservationEncoder, ICnnSweepable
{
    private const int DefaultBucketCount = 20; // matches TickVolumeProfileChart's default bucket count
    private const int DefaultLookbackBars = 50;
    private const int DefaultZScoreSubWindowSize = 5;
    private const int SummaryScalarCount = 3; // POC, value-area-high, value-area-low
    private const double ValueAreaFraction = 0.70;

    private readonly int _bucketCount;
    private readonly int _bucketFeatureCount; // slots/bucket: 1 (Normalized), 2 (Raw), 3 (Normalized+SplitUpDown), or 4 (Raw+SplitUpDown)
    private readonly int _lookbackBars;
    private readonly NormalizationKind _normalization;
    private readonly int _zScoreSubWindowSize;
    private readonly bool _splitUpDown;
    private readonly IFeatureNormalizer _bucketNormalizer;
    private readonly IFeatureNormalizer _priceNormalizer;
    private readonly IReadOnlyList<FeatureDescriptor> _features;

    public VolumeProfileEncoder(
        int bucketCount = DefaultBucketCount, int lookbackBars = DefaultLookbackBars, NormalizationKind normalization = NormalizationKind.Normalized,
        bool splitUpDown = false, int zScoreSubWindowSize = DefaultZScoreSubWindowSize,
        IFeatureNormalizer? bucketNormalizer = null, IFeatureNormalizer? priceNormalizer = null)
    {
        _bucketCount = bucketCount;
        _lookbackBars = lookbackBars;
        _normalization = normalization;
        _zScoreSubWindowSize = zScoreSubWindowSize;
        _splitUpDown = splitUpDown;
        _bucketNormalizer = bucketNormalizer ?? (normalization switch
        {
            NormalizationKind.Raw => new RawValueNormalizer(),
            NormalizationKind.LocalZScore or NormalizationKind.GlobalZScore => new ZScoreNormalizer(),
            NormalizationKind.Binary => new BinaryNormalizer(),
            _ => new FractionNormalizer(),
        });
        _priceNormalizer = priceNormalizer ?? (normalization == NormalizationKind.Raw ? new RawPriceNormalizer() : new PriceOffsetNormalizer());

        if (normalization == NormalizationKind.Raw)
        {
            _bucketFeatureCount = bucketCount * (splitUpDown ? 4 : 2);
            _features = Enumerable.Range(0, _bucketCount)
                .SelectMany(i => splitUpDown
                    ? (FeatureDescriptor[])
                    [
                        new($"vpr-price-{i}", _priceNormalizer),
                        new($"vpr-up-{i}", _bucketNormalizer),
                        new($"vpr-down-{i}", _bucketNormalizer),
                        new($"vpr-diff-{i}", _bucketNormalizer),
                    ]
                    :
                    [
                        new($"vpr-price-{i}", _priceNormalizer),
                        new($"vpr-vol-{i}", _bucketNormalizer),
                    ])
                .Append(new FeatureDescriptor("vpr-poc", _priceNormalizer))
                .Append(new FeatureDescriptor("vpr-va-high", _priceNormalizer))
                .Append(new FeatureDescriptor("vpr-va-low", _priceNormalizer))
                .ToList();
        }
        else
        {
            _bucketFeatureCount = bucketCount * (splitUpDown ? 3 : 1);
            _features = Enumerable.Range(0, _bucketCount)
                .SelectMany(i => splitUpDown
                    ? (FeatureDescriptor[])
                    [
                        new($"vp-up-{i}", _bucketNormalizer),
                        new($"vp-down-{i}", _bucketNormalizer),
                        new($"vp-diff-{i}", _bucketNormalizer),
                    ]
                    : [new($"vp-{i}", _bucketNormalizer)])
                .Append(new FeatureDescriptor("poc-offset", _priceNormalizer))
                .Append(new FeatureDescriptor("va-high-offset", _priceNormalizer))
                .Append(new FeatureDescriptor("va-low-offset", _priceNormalizer))
                .ToList();
        }
    }

    public int FeatureCount => _bucketFeatureCount + SummaryScalarCount;

    public int SequenceLength => 0;

    public int SweepLength => _bucketFeatureCount;


    public int WarmupBars => _lookbackBars;

    public IReadOnlyList<FeatureDescriptor> Features => _features;

    public float[] Encode(SimulationObservation observation)
    {
        var merged = BarPriceHistogram.Merge(observation.RecentBarHistograms, _lookbackBars);
        double bucketWidth = observation.RecentBarHistograms.Count > 0
            ? observation.RecentBarHistograms[^1].BucketWidth
            : 0;

        var features = new float[FeatureCount];

        if (_normalization == NormalizationKind.Raw && _splitUpDown)
        {
            var (up, down, diff, prices) = VolumeProfileAnalysis.BuildRawSplitHistogram(merged, _bucketCount);
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
            var (counts, prices) = VolumeProfileAnalysis.BuildRawHistogram(merged, _bucketCount);
            for (int i = 0; i < _bucketCount; i++)
            {
                features[i * 2] = _priceNormalizer.Normalize(prices[i], observation);
                features[i * 2 + 1] = _bucketNormalizer.Normalize(counts[i], observation);
            }
        }
        else if (_splitUpDown)
        {
            var (up, down, diff) = VolumeProfileAnalysis.BuildRelativeSplitHistogram(merged, _bucketCount);
            if (_normalization is NormalizationKind.LocalZScore or NormalizationKind.GlobalZScore)
            {
                up = ApplyZScore(up);
                down = ApplyZScore(down);
                diff = ApplyZScore(diff);
            }
            else if (_normalization == NormalizationKind.Binary)
            {
                up = ApplyBinary(up);
                down = ApplyBinary(down);
                diff = ApplyBinary(diff);
            }
            for (int i = 0; i < _bucketCount; i++)
            {
                features[i * 3] = _bucketNormalizer.Normalize(up[i], observation);
                features[i * 3 + 1] = _bucketNormalizer.Normalize(down[i], observation);
                features[i * 3 + 2] = _bucketNormalizer.Normalize(diff[i], observation);
            }
        }
        else
        {
            var histogram = VolumeProfileAnalysis.BuildRelativeHistogram(merged, _bucketCount);
            if (_normalization is NormalizationKind.LocalZScore or NormalizationKind.GlobalZScore)
                histogram = ApplyZScore(histogram);
            else if (_normalization == NormalizationKind.Binary)
                histogram = ApplyBinary(histogram);
            for (int i = 0; i < _bucketCount; i++)
                features[i] = _bucketNormalizer.Normalize(histogram[i], observation);
        }

        if (VolumeProfileAnalysis.ComputeSummary(merged, bucketWidth, ValueAreaFraction) is { } summary)
        {
            features[_bucketFeatureCount + 0] = _priceNormalizer.Normalize(summary.PointOfControl, observation);
            features[_bucketFeatureCount + 1] = _priceNormalizer.Normalize(summary.ValueAreaHigh, observation);
            features[_bucketFeatureCount + 2] = _priceNormalizer.Normalize(summary.ValueAreaLow, observation);
        }

        return features;
    }


    private float[] ApplyZScore(float[] values) =>
        _normalization == NormalizationKind.GlobalZScore
            ? ZScoreNormalization.Global(Array.ConvertAll(values, v => (double)v))
            : ZScoreNormalization.Local(Array.ConvertAll(values, v => (double)v), _zScoreSubWindowSize);


    private static float[] ApplyBinary(float[] values) => BinaryNormalization.Compute(Array.ConvertAll(values, v => (double)v));
}
