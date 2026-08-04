// ── Normalizer: passthrough for an already-computed up/down binary value ─
//  Used by BarWindowEncoder/VolumeProfileEncoder's Binary mode -- the 0/1
//  value is already computed against its predecessor before this runs (see
//  BinaryNormalization), so there's nothing left to normalize; Normalize is
//  a plain float cast, same passthrough treatment as ZScoreNormalizer.
//  Denormalize always returns null -- a binary up/down flag can't be
//  inverted back to its raw quantity, so BrainIoPanel's de-normalized
//  display falls back to the plain normalized (0/1) value, same as any
//  other anchor-unavailable case.
// ─────────────────────────────────────────────────────────────────────────
using System.Globalization;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class BinaryNormalizer : IFeatureNormalizer
{
    public float Normalize(double rawValue, SimulationObservation observation) => (float)rawValue;

    public double? Denormalize(float normalizedValue, DenormalizationAnchors anchors) => null;

    public string Format(double rawValue) => rawValue.ToString("0", CultureInfo.InvariantCulture);
}
