// ── Normalizer: price <-> percent-change-from-current-price ─────────────
//  Used by every price-level feature (BarWindowEncoder's t-N, VolumeProfileEncoder/
//  TimeProfileEncoder's poc/va-*-offsets) so a network sees the same kind of
//  bounded, scale-free number regardless of the instrument's absolute price
//  level. currentPrice == 0 (no data yet) normalizes to 0 rather than
//  dividing by zero -- same neutral convention every encoder already used
//  inline before this was pulled out.
// ─────────────────────────────────────────────────────────────────────────
using System.Globalization;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class PriceOffsetNormalizer : IFeatureNormalizer
{
    public float Normalize(double rawValue, SimulationObservation observation) =>
        observation.CurrentPrice == 0 ? 0f : (float)((rawValue - observation.CurrentPrice) / observation.CurrentPrice);

    public double? Denormalize(float normalizedValue, DenormalizationAnchors anchors) =>
        anchors.CurrentPrice is { } currentPrice ? currentPrice * (1 + normalizedValue) : null;

    public string Format(double rawValue) => rawValue.ToString("F5", CultureInfo.InvariantCulture);
}
