using System.Globalization;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class BinaryNormalizer : IFeatureNormalizer
{
	public float Normalize(double rawValue, SimulationObservation observation) => (float)rawValue;

	public double? Denormalize(float normalizedValue, DenormalizationAnchors anchors) => null;

	public string Format(double rawValue) => rawValue.ToString("0", CultureInfo.InvariantCulture);
}
