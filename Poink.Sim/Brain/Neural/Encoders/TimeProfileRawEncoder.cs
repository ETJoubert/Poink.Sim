// ── Encoder: time profile histogram + summary, unnormalized ─────────────
//  Raw-value counterpart to TimeProfileEncoder -- same trailing LookbackBars
//  time-weighted merge (see BarPriceHistogram.MergeTime), but every value
//  fed to the network is the literal seconds spent / price level rather
//  than a fraction-of-total / percent-offset-from-current-price. LookbackBars
//  is settable (IHasLookbackBars), same as TimeProfileEncoder's -- shared
//  with VolumeProfileRawEncoder/VolumeProfileEncoder when composed together
//  (see ComposedPolicyBrain.Create).
//
//  Each bucket emits its exact price *and* its exact seconds-spent, for the
//  same reason as VolumeProfileRawEncoder: a bucket's price isn't
//  recoverable from its index, since the rebin's own [min, max] range shifts
//  with whatever the lookback window happens to span. Without it, the
//  network can't tell where the current price (via TraderStateEncoder) sits
//  against this distribution, or that the whole distribution just shifted
//  because price moved outside the previous window.
//
//  Feature labels are prefixed "tpr-" so this can be composed alongside
//  TimeProfileEncoder ("tp-" labels) and VolumeProfileRawEncoder ("vpr-"
//  labels) in the same brain without collision (see CompositeEncoder).
//
//  SequenceLength = 0 -- same reasoning as TimeProfileEncoder.
// ─────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class TimeProfileRawEncoder : IObservationEncoder, IHasLookbackBars
{
    private const int HistogramBucketCount = 20; // matches TimeProfileEncoder's bucket count
    private const int BucketFeatureCount = HistogramBucketCount * 2; // price + seconds per bucket
    private const int SummaryScalarCount = 3; // POC, value-area-high, value-area-low
    private const double ValueAreaFraction = 0.70;

    private readonly IFeatureNormalizer _secondsNormalizer;
    private readonly IFeatureNormalizer _priceNormalizer;
    private readonly IReadOnlyList<FeatureDescriptor> _features;

    public TimeProfileRawEncoder(IFeatureNormalizer? secondsNormalizer = null, IFeatureNormalizer? priceNormalizer = null)
    {
        _secondsNormalizer = secondsNormalizer ?? new RawValueNormalizer();
        _priceNormalizer = priceNormalizer ?? new RawPriceNormalizer();
        _features = Enumerable.Range(0, HistogramBucketCount)
            .SelectMany(i => new[]
            {
                new FeatureDescriptor($"tpr-price-{i}", _priceNormalizer),
                new FeatureDescriptor($"tpr-sec-{i}", _secondsNormalizer),
            })
            .Append(new FeatureDescriptor("tpr-poc", _priceNormalizer))
            .Append(new FeatureDescriptor("tpr-va-high", _priceNormalizer))
            .Append(new FeatureDescriptor("tpr-va-low", _priceNormalizer))
            .ToList();
    }

    // How many trailing completed bars' histograms to merge into one profile
    // each decision -- see IHasLookbackBars's doc comment; live Simulation
    // playback uses whatever was last configured.
    public int LookbackBars { get; set; } = 50;

    public int FeatureCount => BucketFeatureCount + SummaryScalarCount;

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

        var (seconds, prices) = TimeProfileAnalysis.BuildRawHistogram(merged, HistogramBucketCount);
        for (int i = 0; i < HistogramBucketCount; i++)
        {
            features[i * 2] = _priceNormalizer.Normalize(prices[i], observation);
            features[i * 2 + 1] = _secondsNormalizer.Normalize(seconds[i], observation);
        }

        // No summary (empty/zero-time window) leaves these three at their
        // array-initialized 0f -- neutral, same "no false signal" convention
        // BuildRawHistogram's own empty-input case uses.
        if (TimeProfileAnalysis.ComputeSummary(merged, bucketWidth, ValueAreaFraction) is { } summary)
        {
            features[BucketFeatureCount + 0] = _priceNormalizer.Normalize(summary.PointOfControl, observation);
            features[BucketFeatureCount + 1] = _priceNormalizer.Normalize(summary.ValueAreaHigh, observation);
            features[BucketFeatureCount + 2] = _priceNormalizer.Normalize(summary.ValueAreaLow, observation);
        }

        return features;
    }
}
