// ── Normalizer: passthrough for an already-real, unscaled quantity ──────
//  Used wherever a caller wants the network fed the exact raw value instead
//  of a ratio/fraction/offset -- e.g. VolumeProfileRawEncoder/
//  TimeProfileRawEncoder's histogram buckets (exact tick count / seconds
//  spent) and TraderStateEncoder's account fields (cash, equity, margin,
//  ...). Normalize is a plain float cast; Denormalize is the identity for
//  the same reason as RawPriceNormalizer -- the normalized value already IS
//  the real quantity, no anchor needed to invert it.
// ─────────────────────────────────────────────────────────────────────────
using System.Globalization;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class RawValueNormalizer : IFeatureNormalizer
{
    public float Normalize(double rawValue, SimulationObservation observation) => (float)rawValue;

    public double? Denormalize(float normalizedValue, DenormalizationAnchors anchors) => normalizedValue;

    public string Format(double rawValue) => rawValue.ToString("N2", CultureInfo.InvariantCulture);
}
