
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class DirectionNormalizer : IFeatureNormalizer
{
    public float Normalize(double rawValue, SimulationObservation observation) => (float)rawValue;

    public double? Denormalize(float normalizedValue, DenormalizationAnchors anchors) => normalizedValue;

    public string Format(double rawValue) => rawValue switch { > 0 => "LONG", < 0 => "SHORT", _ => "FLAT" };
}
