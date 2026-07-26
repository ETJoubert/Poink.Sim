// ── Normalizer: passthrough for a categorical -1/0/1 direction flag ─────
//  Used by TraderStateEncoder's "position" feature -- long/short/flat is
//  already categorical, not a raw value plus anchor, so there's nothing to
//  scale; Normalize is a plain float cast, same reasoning as
//  FractionNormalizer. Format is the one place this normalizer does real
//  work: turning -1/0/1 back into a direction label for display.
// ─────────────────────────────────────────────────────────────────────────
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class DirectionNormalizer : IFeatureNormalizer
{
    public float Normalize(double rawValue, SimulationObservation observation) => (float)rawValue;

    public double? Denormalize(float normalizedValue, DenormalizationAnchors anchors) => normalizedValue;

    public string Format(double rawValue) => rawValue switch { > 0 => "LONG", < 0 => "SHORT", _ => "FLAT" };
}
