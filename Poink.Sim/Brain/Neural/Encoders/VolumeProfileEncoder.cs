// ── Encoder: volume profile histogram + summary ──────────────────────────
//  Extracted from the formerly-standalone VolumeProfilePolicyBrain -- merges
//  this encoder's own trailing LookbackBars completed bars' price histograms
//  (see BarPriceHistogram.Merge) into a fixed-width relative histogram plus
//  point-of-control/value-area summary scalars (see VolumeProfileAnalysis).
//  LookbackBars is settable (IHasLookbackBars) -- RlTrainingService assigns
//  it once at training start from the Train page's form (see
//  ComposedPolicyBrain.Create/LookbackAwarePolicyBrain for how this
//  propagates from a composed brain down to whichever of its parts actually
//  implements IHasLookbackBars); the network's input width stays fixed
//  regardless (HistogramBucketCount never changes).
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

internal sealed class VolumeProfileEncoder : IObservationEncoder, IHasLookbackBars
{
    private const int HistogramBucketCount = 20; // matches TickVolumeProfileChart's default bucket count
    private const int SummaryScalarCount = 3; // POC offset, value-area-high offset, value-area-low offset
    private const double ValueAreaFraction = 0.70;

    private readonly IFeatureNormalizer _fractionNormalizer;
    private readonly IFeatureNormalizer _priceOffsetNormalizer;
    private readonly IReadOnlyList<FeatureDescriptor> _features;

    public VolumeProfileEncoder(IFeatureNormalizer? fractionNormalizer = null, IFeatureNormalizer? priceOffsetNormalizer = null)
    {
        _fractionNormalizer = fractionNormalizer ?? new FractionNormalizer();
        _priceOffsetNormalizer = priceOffsetNormalizer ?? new PriceOffsetNormalizer();
        _features = Enumerable.Range(0, HistogramBucketCount)
            .Select(i => new FeatureDescriptor($"vp-{i}", _fractionNormalizer))
            .Append(new FeatureDescriptor("poc-offset", _priceOffsetNormalizer))
            .Append(new FeatureDescriptor("va-high-offset", _priceOffsetNormalizer))
            .Append(new FeatureDescriptor("va-low-offset", _priceOffsetNormalizer))
            .ToList();
    }

    // How many trailing completed bars' histograms to merge into one profile
    // each decision -- see IHasLookbackBars's doc comment; live Simulation
    // playback uses whatever was last configured.
    public int LookbackBars { get; set; } = 50;

    public int FeatureCount => HistogramBucketCount + SummaryScalarCount;

    public int SequenceLength => 0;

    // Same number as LookbackBars itself -- this encoder's own trailing-merge
    // window IS its warm-up requirement (see IObservationEncoder.WarmupBars).
    public int WarmupBars => LookbackBars;

    public IReadOnlyList<FeatureDescriptor> Features => _features;

    public float[] Encode(SimulationObservation observation)
    {
        var merged = BarPriceHistogram.Merge(observation.RecentBarHistograms, LookbackBars);
        double bucketWidth = observation.RecentBarHistograms.Count > 0
            ? observation.RecentBarHistograms[^1].BucketWidth
            : 0;

        var features = new float[FeatureCount];

        var histogram = VolumeProfileAnalysis.BuildRelativeHistogram(merged, HistogramBucketCount);
        for (int i = 0; i < HistogramBucketCount; i++)
            features[i] = _fractionNormalizer.Normalize(histogram[i], observation);

        // No summary (empty/zero-volume window) leaves these three at their
        // array-initialized 0f -- neutral, same "no false signal" convention
        // BuildRelativeHistogram's own empty-input case uses.
        if (VolumeProfileAnalysis.ComputeSummary(merged, bucketWidth, ValueAreaFraction) is { } summary)
        {
            features[HistogramBucketCount + 0] = _priceOffsetNormalizer.Normalize(summary.PointOfControl, observation);
            features[HistogramBucketCount + 1] = _priceOffsetNormalizer.Normalize(summary.ValueAreaHigh, observation);
            features[HistogramBucketCount + 2] = _priceOffsetNormalizer.Normalize(summary.ValueAreaLow, observation);
        }

        return features;
    }
}
