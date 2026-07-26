// ── Normalizer: passthrough for an already-computed [0,1] fraction ──────
//  Used by VolumeProfileEncoder/TimeProfileEncoder's histogram buckets --
//  each bucket's value is already a share of a merged distribution (volume
//  or seconds), not a single raw value plus anchor, so there's nothing left
//  to normalize; Normalize is a plain float cast. Kept as a normalizer (not
//  a special-cased skip) so every emitted feature still has one, uniformly,
//  for TrainLiveTab's display formatting. Denormalizing to a raw tick count
//  or seconds figure would need re-merging BarPriceHistograms -- not worth
//  it for a fraction that already reads fine as a percentage.
// ─────────────────────────────────────────────────────────────────────────
using System.Globalization;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class FractionNormalizer : IFeatureNormalizer
{
    public float Normalize(double rawValue, SimulationObservation observation) => (float)rawValue;

    public double? Denormalize(float normalizedValue, DenormalizationAnchors anchors) => normalizedValue;

    public string Format(double rawValue) => (rawValue * 100).ToString("N1", CultureInfo.InvariantCulture) + "%";
}
