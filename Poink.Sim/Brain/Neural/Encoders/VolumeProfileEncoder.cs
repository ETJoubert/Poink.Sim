// ── Encoder: volume profile histogram + summary ──────────────────────────
//  Merges this encoder's own trailing LookbackBars completed bars' price
//  histograms (see BarPriceHistogram.Merge) into a fixed-width volume
//  profile plus point-of-control/value-area summary scalars (see
//  VolumeProfileAnalysis). LookbackBars is a fixed, build-time value
//  (constructor-supplied, defaulting to 50) baked in at construction --
//  same treatment BucketCount always had -- not a runtime-mutable setting;
//  the network's input width stays fixed regardless (FeatureCount never
//  changes after construction).
//
//  Normalization picks the feature convention:
//  - Normalized (default): each bucket is a fraction-of-total-volume
//    (FractionNormalizer), summary scalars are percent-offset-from-current-
//    price (PriceOffsetNormalizer) -- "vp-{i}"/"poc-offset"/"va-*-offset"
//    labels, FeatureCount = bucketCount + 3.
//  - Raw: each bucket emits its exact price *and* its exact volume, not
//    volume alone -- a bucket's price isn't recoverable from its index,
//    since the rebin's own [min, max] price range shifts with whatever the
//    lookback window happens to span, so bucket i sits at a different real
//    price every decision. Without the price alongside the count, the
//    network (which sees the actual current price via TraderStateEncoder)
//    has no way to tell whether current price falls inside this
//    distribution at all, let alone which bucket -- "vpr-price-{i}"/
//    "vpr-vol-{i}"/"vpr-poc"/"vpr-va-*" labels (RawPriceNormalizer/
//    RawValueNormalizer), FeatureCount = bucketCount*2 + 3. Kept as a
//    distinct label prefix (not reused from Normalized mode) purely so a
//    de-normalized display can tell at a glance which convention produced
//    a given feature; the two modes are never selected simultaneously in
//    one brain, so there's no actual collision to avoid.
//
//  SplitUpDown (default off, orthogonal to Normalization): instead of one
//  plain volume value per bucket, reports its up-tick and down-tick volume
//  separately plus their Diff (Up - Down) -- the histogram counterpart to
//  TickVolumeProfileChart's own "Split up/down" toggle, letting the network
//  see which side of each price level actually traded rather than just how
//  much. Triples each bucket's slot count (Normalized: "vp-up-{i}"/
//  "vp-down-{i}"/"vp-diff-{i}"; Raw: "vpr-price-{i}"/"vpr-up-{i}"/
//  "vpr-down-{i}"/"vpr-diff-{i}", price still one slot since it locates all
//  three counts at once) -- FeatureCount = bucketCount*3 + 3 (Normalized) or
//  bucketCount*4 + 3 (Raw). Diff can read negative (down-skewed bucket), so
//  it rides the same normalizer as Up/Down (FractionNormalizer/
//  RawValueNormalizer are both plain float casts -- neither clamps to
//  non-negative) rather than needing one of its own.
//
//  SequenceLength = 0 -- the histogram buckets are price-ordered but this
//  encoder doesn't expose that as a recurrent-architecture sequence today;
//  if paired with LstmArchitecture it must not be listed first (see
//  CompositeEncoder's ordering guard).
// ─────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class VolumeProfileEncoder : IObservationEncoder
{
    private const int DefaultBucketCount = 20; // matches TickVolumeProfileChart's default bucket count
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

    public VolumeProfileEncoder(
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

    // Same number as LookbackBars -- this encoder's own trailing-merge
    // window IS its warm-up requirement (see IObservationEncoder.WarmupBars).
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
            for (int i = 0; i < _bucketCount; i++)
                features[i] = _bucketNormalizer.Normalize(histogram[i], observation);
        }

        // No summary (empty/zero-volume window) leaves these three at their
        // array-initialized 0f -- neutral, same "no false signal" convention
        // BuildRelativeHistogram/BuildRawHistogram's own empty-input case uses.
        if (VolumeProfileAnalysis.ComputeSummary(merged, bucketWidth, ValueAreaFraction) is { } summary)
        {
            features[_bucketFeatureCount + 0] = _priceNormalizer.Normalize(summary.PointOfControl, observation);
            features[_bucketFeatureCount + 1] = _priceNormalizer.Normalize(summary.ValueAreaHigh, observation);
            features[_bucketFeatureCount + 2] = _priceNormalizer.Normalize(summary.ValueAreaLow, observation);
        }

        return features;
    }
}
