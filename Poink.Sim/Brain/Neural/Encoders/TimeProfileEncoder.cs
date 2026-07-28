// ── Encoder: time profile histogram + summary ────────────────────────────
//  Time-weighted counterpart to VolumeProfileEncoder -- merges this
//  encoder's own trailing LookbackBars completed bars' price histograms
//  (see BarPriceHistogram.MergeTime) into a fixed-width seconds-spent
//  profile plus point-of-control/value-area summary scalars (see
//  TimeProfileAnalysis), instead of volume profile's tick-count weighting
//  -- tells apart "many ticks in an instant" from "price parked here a
//  while". LookbackBars is a fixed, build-time value (constructor-supplied,
//  defaulting to 50) baked in at construction -- same treatment BucketCount
//  always had -- not a runtime-mutable setting; Volume Profile and Time
//  Profile no longer share one lookback window when combined in the same
//  brain, each gets its own independent value.
//
//  Normalization picks the feature convention (mirrors VolumeProfileEncoder):
//  - Normalized (default): each bucket is a fraction-of-total-seconds
//    (FractionNormalizer), summary scalars are percent-offset-from-current-
//    price (PriceOffsetNormalizer) -- "tp-{i}"/"tp-poc-offset"/
//    "tp-va-*-offset" labels, FeatureCount = bucketCount + 3.
//  - Raw: each bucket emits its exact price *and* its exact seconds-spent,
//    for the same reason as VolumeProfileEncoder's Raw mode -- a bucket's
//    price isn't recoverable from its index, since the rebin's own
//    [min, max] range shifts with whatever the lookback window happens to
//    span -- "tpr-price-{i}"/"tpr-sec-{i}"/"tpr-poc"/"tpr-va-*" labels
//    (RawPriceNormalizer/RawValueNormalizer), FeatureCount = bucketCount*2+3.
//
//  SequenceLength = 0 -- same reasoning as VolumeProfileEncoder: the
//  histogram buckets are price-ordered but this encoder doesn't expose that
//  as a recurrent-architecture sequence today; if paired with
//  LstmArchitecture it must not be listed first (see CompositeEncoder's
//  ordering guard).
// ─────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class TimeProfileEncoder : IObservationEncoder
{
    private const int DefaultBucketCount = 20; // matches VolumeProfileEncoder's bucket count
    private const int DefaultLookbackBars = 50;
    private const int SummaryScalarCount = 3; // POC, value-area-high, value-area-low
    private const double ValueAreaFraction = 0.70;

    private readonly int _bucketCount;
    private readonly int _bucketFeatureCount; // 1 slot/bucket (Normalized) or 2 (Raw: price + seconds)
    private readonly int _lookbackBars;
    private readonly NormalizationKind _normalization;
    private readonly IFeatureNormalizer _bucketNormalizer;
    private readonly IFeatureNormalizer _priceNormalizer;
    private readonly IReadOnlyList<FeatureDescriptor> _features;

    public TimeProfileEncoder(
        int bucketCount = DefaultBucketCount, int lookbackBars = DefaultLookbackBars, NormalizationKind normalization = NormalizationKind.Normalized,
        IFeatureNormalizer? bucketNormalizer = null, IFeatureNormalizer? priceNormalizer = null)
    {
        _bucketCount = bucketCount;
        _lookbackBars = lookbackBars;
        _normalization = normalization;
        _bucketNormalizer = bucketNormalizer ?? (normalization == NormalizationKind.Raw ? new RawValueNormalizer() : new FractionNormalizer());
        _priceNormalizer = priceNormalizer ?? (normalization == NormalizationKind.Raw ? new RawPriceNormalizer() : new PriceOffsetNormalizer());

        if (normalization == NormalizationKind.Raw)
        {
            _bucketFeatureCount = bucketCount * 2;
            _features = Enumerable.Range(0, _bucketCount)
                .SelectMany(i => new[]
                {
                    new FeatureDescriptor($"tpr-price-{i}", _priceNormalizer),
                    new FeatureDescriptor($"tpr-sec-{i}", _bucketNormalizer),
                })
                .Append(new FeatureDescriptor("tpr-poc", _priceNormalizer))
                .Append(new FeatureDescriptor("tpr-va-high", _priceNormalizer))
                .Append(new FeatureDescriptor("tpr-va-low", _priceNormalizer))
                .ToList();
        }
        else
        {
            _bucketFeatureCount = bucketCount;
            _features = Enumerable.Range(0, _bucketCount)
                .Select(i => new FeatureDescriptor($"tp-{i}", _bucketNormalizer))
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
        var merged = BarPriceHistogram.MergeTime(observation.RecentBars, observation.RecentBarHistograms, _lookbackBars);
        double bucketWidth = observation.RecentBarHistograms.Count > 0
            ? observation.RecentBarHistograms[^1].BucketWidth
            : 0;

        var features = new float[FeatureCount];

        if (_normalization == NormalizationKind.Raw)
        {
            var (seconds, prices) = TimeProfileAnalysis.BuildRawHistogram(merged, _bucketCount);
            for (int i = 0; i < _bucketCount; i++)
            {
                features[i * 2] = _priceNormalizer.Normalize(prices[i], observation);
                features[i * 2 + 1] = _bucketNormalizer.Normalize(seconds[i], observation);
            }
        }
        else
        {
            var histogram = TimeProfileAnalysis.BuildRelativeHistogram(merged, _bucketCount);
            for (int i = 0; i < _bucketCount; i++)
                features[i] = _bucketNormalizer.Normalize(histogram[i], observation);
        }

        // No summary (empty/zero-time window) leaves these three at their
        // array-initialized 0f -- neutral, same "no false signal" convention
        // BuildRelativeHistogram/BuildRawHistogram's own empty-input case uses.
        if (TimeProfileAnalysis.ComputeSummary(merged, bucketWidth, ValueAreaFraction) is { } summary)
        {
            features[_bucketFeatureCount + 0] = _priceNormalizer.Normalize(summary.PointOfControl, observation);
            features[_bucketFeatureCount + 1] = _priceNormalizer.Normalize(summary.ValueAreaHigh, observation);
            features[_bucketFeatureCount + 2] = _priceNormalizer.Normalize(summary.ValueAreaLow, observation);
        }

        return features;
    }
}
