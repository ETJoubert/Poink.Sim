
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

public readonly record struct DenormalizationAnchors(double? CurrentPrice, double? Equity);

public interface IFeatureDisplayNormalizer
{
    double? Denormalize(float normalizedValue, DenormalizationAnchors anchors);

    string Format(double rawValue);
}

internal interface IFeatureNormalizer : IFeatureDisplayNormalizer
{
    float Normalize(double rawValue, SimulationObservation observation);
}
