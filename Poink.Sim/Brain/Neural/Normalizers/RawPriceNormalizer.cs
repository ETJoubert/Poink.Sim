// ── Normalizer: passthrough for an already-real, unscaled price level ───
//  Used where a caller wants the network fed the literal price (e.g.
//  VolumeProfileEncoder/TimeProfileEncoder's Raw-mode POC/value-area summary,
//  TraderStateEncoder's current price) instead of PriceOffsetNormalizer's
//  percent-change-from-current-price encoding. Normalize is a plain float
//  cast; Denormalize is the identity for the same reason -- the normalized
//  value already IS the real price, no anchor needed to invert it.
// ─────────────────────────────────────────────────────────────────────────
using System.Globalization;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class RawPriceNormalizer : IFeatureNormalizer
{
    public float Normalize(double rawValue, SimulationObservation observation) => (float)rawValue;

    public double? Denormalize(float normalizedValue, DenormalizationAnchors anchors) => normalizedValue;

    public string Format(double rawValue) => rawValue.ToString("F5", CultureInfo.InvariantCulture);
}
