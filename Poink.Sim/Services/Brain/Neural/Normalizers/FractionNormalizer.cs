using System.Globalization;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class FractionNormalizer : IFeatureNormalizer
{
	public float Normalize(double rawValue, SimulationObservation observation) => (float)rawValue;

	public double? Denormalize(float normalizedValue, DenormalizationAnchors anchors) => normalizedValue;

	public string Format(double rawValue) => (rawValue * 100).ToString("N1", CultureInfo.InvariantCulture) + "%";
}
