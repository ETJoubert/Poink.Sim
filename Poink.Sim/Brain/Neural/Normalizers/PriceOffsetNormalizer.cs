
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
