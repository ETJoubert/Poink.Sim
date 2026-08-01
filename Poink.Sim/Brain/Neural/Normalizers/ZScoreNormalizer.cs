// ── Normalizer: passthrough for an already-computed Local/Global Z-Score ──
//  Used by BarWindowEncoder/VolumeProfileEncoder's LocalZScore/GlobalZScore
//  modes -- the z-score is already computed against its window's own
//  mean/stddev before this runs (see ZScoreNormalization), so there's
//  nothing left to normalize; Normalize is a plain float cast, same
//  passthrough treatment as FractionNormalizer/RawValueNormalizer. Denormalize
//  always returns null -- unlike PriceOffsetNormalizer's single CurrentPrice
//  anchor, a z-score can only be inverted back to its raw quantity with the
//  exact mean/stddev of the window it was computed against, which isn't
//  carried anywhere past encode-time -- so BrainIoPanel's de-normalized
//  display falls back to the plain normalized (z-score) value, same as any
//  other anchor-unavailable case.
// ─────────────────────────────────────────────────────────────────────────
using System.Globalization;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class ZScoreNormalizer : IFeatureNormalizer
{
    public float Normalize(double rawValue, SimulationObservation observation) => (float)rawValue;

    public double? Denormalize(float normalizedValue, DenormalizationAnchors anchors) => null;

    public string Format(double rawValue) => rawValue.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture);
}
