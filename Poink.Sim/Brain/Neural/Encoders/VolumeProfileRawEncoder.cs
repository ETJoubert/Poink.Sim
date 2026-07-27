// ── Encoder: volume profile histogram + summary, unnormalized ───────────
//  Raw-value counterpart to VolumeProfileEncoder -- same trailing
//  LookbackBars completed bars' price histograms (see BarPriceHistogram.
//  Merge), but every value fed to the network is the literal tick count /
//  price level rather than a fraction-of-total / percent-offset-from-
//  current-price, for a brain that should see exactly how many ticks traded
//  and exactly where, not a scale-free share of them. LookbackBars is
//  settable (IHasLookbackBars) same as VolumeProfileEncoder's.
//
//  Each bucket emits its exact price *and* its exact volume, not volume
//  alone -- a bucket's price isn't recoverable from its index, since the
//  rebin's own [min, max] price range shifts with whatever the lookback
//  window happens to span, so bucket i sits at a different real price every
//  decision. Without the price alongside the count, the network (which sees
//  the actual current price via TraderStateEncoder) has no way to tell
//  whether current price falls inside this distribution at all, let alone
//  which bucket -- and once price moves outside the window entirely, every
//  bucket's price and volume shift together on the next decision.
//
//  Feature labels are prefixed "vpr-" so this can be composed alongside
//  VolumeProfileEncoder ("vp-" labels) in the same brain without collision
//  (see CompositeEncoder).
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

internal sealed class VolumeProfileRawEncoder : IObservationEncoder, IHasLookbackBars
{
    private const int DefaultBucketCount = 20; // matches VolumeProfileEncoder's bucket count
    private const int SummaryScalarCount = 3; // POC, value-area-high, value-area-low
    private const double ValueAreaFraction = 0.70;

    private readonly int _bucketCount;
    private readonly int _bucketFeatureCount; // price + volume per bucket
    private readonly IFeatureNormalizer _countNormalizer;
    private readonly IFeatureNormalizer _priceNormalizer;
    private readonly IReadOnlyList<FeatureDescriptor> _features;

    public VolumeProfileRawEncoder(int bucketCount = DefaultBucketCount, IFeatureNormalizer? countNormalizer = null, IFeatureNormalizer? priceNormalizer = null)
    {
        _bucketCount = bucketCount;
        _bucketFeatureCount = bucketCount * 2;
        _countNormalizer = countNormalizer ?? new RawValueNormalizer();
        _priceNormalizer = priceNormalizer ?? new RawPriceNormalizer();
        _features = Enumerable.Range(0, _bucketCount)
            .SelectMany(i => new[]
            {
                new FeatureDescriptor($"vpr-price-{i}", _priceNormalizer),
                new FeatureDescriptor($"vpr-vol-{i}", _countNormalizer),
            })
            .Append(new FeatureDescriptor("vpr-poc", _priceNormalizer))
            .Append(new FeatureDescriptor("vpr-va-high", _priceNormalizer))
            .Append(new FeatureDescriptor("vpr-va-low", _priceNormalizer))
            .ToList();
    }

    // How many trailing completed bars' histograms to merge into one profile
    // each decision -- see IHasLookbackBars's doc comment; live Simulation
    // playback uses whatever was last configured.
    public int LookbackBars { get; set; } = 50;

    public int FeatureCount => _bucketFeatureCount + SummaryScalarCount;

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

        var (counts, prices) = VolumeProfileAnalysis.BuildRawHistogram(merged, _bucketCount);
        for (int i = 0; i < _bucketCount; i++)
        {
            features[i * 2] = _priceNormalizer.Normalize(prices[i], observation);
            features[i * 2 + 1] = _countNormalizer.Normalize(counts[i], observation);
        }

        // No summary (empty/zero-volume window) leaves these three at their
        // array-initialized 0f -- neutral, same "no false signal" convention
        // BuildRawHistogram's own empty-input case uses.
        if (VolumeProfileAnalysis.ComputeSummary(merged, bucketWidth, ValueAreaFraction) is { } summary)
        {
            features[_bucketFeatureCount + 0] = _priceNormalizer.Normalize(summary.PointOfControl, observation);
            features[_bucketFeatureCount + 1] = _priceNormalizer.Normalize(summary.ValueAreaHigh, observation);
            features[_bucketFeatureCount + 2] = _priceNormalizer.Normalize(summary.ValueAreaLow, observation);
        }

        return features;
    }
}
