// ── Encoder: time profile histogram + summary ────────────────────────────
//  Time-weighted counterpart to VolumeProfileEncoder -- merges this
//  encoder's own trailing LookbackBars completed bars' price histograms
//  (see BarPriceHistogram.MergeTime) into a fixed-width relative
//  seconds-spent histogram plus point-of-control/value-area summary scalars
//  (see TimeProfileAnalysis), instead of volume profile's tick-count
//  weighting -- tells apart "many ticks in an instant" from "price parked
//  here a while".
//
//  LookbackBars is settable (IHasLookbackBars), same as VolumeProfileEncoder's
//  -- ComposedPolicyBrain.Create broadcasts one configured value to every
//  composed part that implements it (see that file), so Volume Profile and
//  Time Profile can be combined in the same brain and share one lookback
//  window; the network's input width stays fixed regardless (HistogramBucketCount
//  never changes).
//
//  Feature labels are prefixed "tp-" (including the summary scalars) rather
//  than VolumeProfileEncoder's bare "poc-offset"/etc, so the two encoders'
//  labels never collide when CompositeEncoder concatenates both into one
//  brain's flat feature vector.
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

internal sealed class TimeProfileEncoder : IObservationEncoder, IHasLookbackBars
{
    private const int DefaultBucketCount = 20; // matches VolumeProfileEncoder's bucket count
    private const int SummaryScalarCount = 3; // POC offset, value-area-high offset, value-area-low offset
    private const double ValueAreaFraction = 0.70;

    private readonly int _bucketCount;
    private readonly IFeatureNormalizer _fractionNormalizer;
    private readonly IFeatureNormalizer _priceOffsetNormalizer;
    private readonly IReadOnlyList<FeatureDescriptor> _features;

    public TimeProfileEncoder(
        int bucketCount = DefaultBucketCount, IFeatureNormalizer? fractionNormalizer = null, IFeatureNormalizer? priceOffsetNormalizer = null)
    {
        _bucketCount = bucketCount;
        _fractionNormalizer = fractionNormalizer ?? new FractionNormalizer();
        _priceOffsetNormalizer = priceOffsetNormalizer ?? new PriceOffsetNormalizer();
        _features = Enumerable.Range(0, _bucketCount)
            .Select(i => new FeatureDescriptor($"tp-{i}", _fractionNormalizer))
            .Append(new FeatureDescriptor("tp-poc-offset", _priceOffsetNormalizer))
            .Append(new FeatureDescriptor("tp-va-high-offset", _priceOffsetNormalizer))
            .Append(new FeatureDescriptor("tp-va-low-offset", _priceOffsetNormalizer))
            .ToList();
    }

    // How many trailing completed bars' histograms to merge into one profile
    // each decision -- see IHasLookbackBars's doc comment; live Simulation
    // playback uses whatever was last configured.
    public int LookbackBars { get; set; } = 50;

    public int FeatureCount => _bucketCount + SummaryScalarCount;

    public int SequenceLength => 0;

    // Same number as LookbackBars itself -- this encoder's own trailing-merge
    // window IS its warm-up requirement (see IObservationEncoder.WarmupBars).
    public int WarmupBars => LookbackBars;

    public IReadOnlyList<FeatureDescriptor> Features => _features;

    public float[] Encode(SimulationObservation observation)
    {
        var merged = BarPriceHistogram.MergeTime(observation.RecentBars, observation.RecentBarHistograms, LookbackBars);
        double bucketWidth = observation.RecentBarHistograms.Count > 0
            ? observation.RecentBarHistograms[^1].BucketWidth
            : 0;

        var features = new float[FeatureCount];

        var histogram = TimeProfileAnalysis.BuildRelativeHistogram(merged, _bucketCount);
        for (int i = 0; i < _bucketCount; i++)
            features[i] = _fractionNormalizer.Normalize(histogram[i], observation);

        // No summary (empty/zero-time window) leaves these three at their
        // array-initialized 0f -- neutral, same "no false signal" convention
        // BuildRelativeHistogram's own empty-input case uses.
        if (TimeProfileAnalysis.ComputeSummary(merged, bucketWidth, ValueAreaFraction) is { } summary)
        {
            features[_bucketCount + 0] = _priceOffsetNormalizer.Normalize(summary.PointOfControl, observation);
            features[_bucketCount + 1] = _priceOffsetNormalizer.Normalize(summary.ValueAreaHigh, observation);
            features[_bucketCount + 2] = _priceOffsetNormalizer.Normalize(summary.ValueAreaLow, observation);
        }

        return features;
    }
}
