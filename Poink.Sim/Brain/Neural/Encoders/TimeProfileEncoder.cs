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
//  SplitUpDown (default off, orthogonal to Normalization): instead of one
//  plain seconds value per bucket, reports its up-tick and down-tick seconds
//  separately plus their Diff (Up - Down) -- mirrors VolumeProfileEncoder's
//  own SplitUpDown option, just weighted by dwell time instead of tick
//  count. Triples each bucket's slot count (Normalized: "tp-up-{i}"/
//  "tp-down-{i}"/"tp-diff-{i}"; Raw: "tpr-price-{i}"/"tpr-up-{i}"/
//  "tpr-down-{i}"/"tpr-diff-{i}", price still one slot since it locates all
//  three seconds figures at once) -- FeatureCount = bucketCount*3 + 3
//  (Normalized) or bucketCount*4 + 3 (Raw). Diff can read negative
//  (down-skewed bucket), so it rides the same normalizer as Up/Down
//  (FractionNormalizer/RawValueNormalizer are both plain float casts --
//  neither clamps to non-negative) rather than needing one of its own.
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
    private readonly int _bucketFeatureCount; // slots/bucket: 1 (Normalized), 2 (Raw), 3 (Normalized+SplitUpDown), or 4 (Raw+SplitUpDown)
    private readonly int _lookbackBars;
    private readonly NormalizationKind _normalization;
    private readonly bool _splitUpDown;
    private readonly IFeatureNormalizer _bucketNormalizer;
    private readonly IFeatureNormalizer _priceNormalizer;
    private readonly IReadOnlyList<FeatureDescriptor> _features;

    public TimeProfileEncoder(
        int bucketCount = DefaultBucketCount, int lookbackBars = DefaultLookbackBars, NormalizationKind normalization = NormalizationKind.Normalized,
        bool splitUpDown = false, IFeatureNormalizer? bucketNormalizer = null, IFeatureNormalizer? priceNormalizer = null)
    {
        _bucketCount = bucketCount;
        _lookbackBars = lookbackBars;
        _normalization = normalization;
        _splitUpDown = splitUpDown;
        _bucketNormalizer = bucketNormalizer ?? (normalization == NormalizationKind.Raw ? new RawValueNormalizer() : new FractionNormalizer());
        _priceNormalizer = priceNormalizer ?? (normalization == NormalizationKind.Raw ? new RawPriceNormalizer() : new PriceOffsetNormalizer());

        if (normalization == NormalizationKind.Raw)
        {
            _bucketFeatureCount = bucketCount * (splitUpDown ? 4 : 2);
            _features = Enumerable.Range(0, _bucketCount)
                .SelectMany(i => splitUpDown
                    ? (FeatureDescriptor[])
                    [
                        new($"tpr-price-{i}", _priceNormalizer),
                        new($"tpr-up-{i}", _bucketNormalizer),
                        new($"tpr-down-{i}", _bucketNormalizer),
                        new($"tpr-diff-{i}", _bucketNormalizer),
                    ]
                    :
                    [
                        new($"tpr-price-{i}", _priceNormalizer),
                        new($"tpr-sec-{i}", _bucketNormalizer),
                    ])
                .Append(new FeatureDescriptor("tpr-poc", _priceNormalizer))
                .Append(new FeatureDescriptor("tpr-va-high", _priceNormalizer))
                .Append(new FeatureDescriptor("tpr-va-low", _priceNormalizer))
                .ToList();
        }
        else
        {
            _bucketFeatureCount = bucketCount * (splitUpDown ? 3 : 1);
            _features = Enumerable.Range(0, _bucketCount)
                .SelectMany(i => splitUpDown
                    ? (FeatureDescriptor[])
                    [
                        new($"tp-up-{i}", _bucketNormalizer),
                        new($"tp-down-{i}", _bucketNormalizer),
                        new($"tp-diff-{i}", _bucketNormalizer),
                    ]
                    : [new($"tp-{i}", _bucketNormalizer)])
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

        if (_normalization == NormalizationKind.Raw && _splitUpDown)
        {
            var (up, down, diff, prices) = TimeProfileAnalysis.BuildRawSplitHistogram(merged, _bucketCount);
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
            var (seconds, prices) = TimeProfileAnalysis.BuildRawHistogram(merged, _bucketCount);
            for (int i = 0; i < _bucketCount; i++)
            {
                features[i * 2] = _priceNormalizer.Normalize(prices[i], observation);
                features[i * 2 + 1] = _bucketNormalizer.Normalize(seconds[i], observation);
            }
        }
        else if (_splitUpDown)
        {
            var (up, down, diff) = TimeProfileAnalysis.BuildRelativeSplitHistogram(merged, _bucketCount);
            for (int i = 0; i < _bucketCount; i++)
            {
                features[i * 3] = _bucketNormalizer.Normalize(up[i], observation);
                features[i * 3 + 1] = _bucketNormalizer.Normalize(down[i], observation);
                features[i * 3 + 2] = _bucketNormalizer.Normalize(diff[i], observation);
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
