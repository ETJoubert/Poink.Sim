using System.Globalization;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class EquityRatioNormalizer : IFeatureNormalizer
{
	public float Normalize(double rawValue, SimulationObservation observation) =>
		observation.Capital.Equity == 0 ? 0f : (float)(rawValue / observation.Capital.Equity);

	public double? Denormalize(float normalizedValue, DenormalizationAnchors anchors) =>
		anchors.Equity is { } equity ? normalizedValue * equity : null;

	public string Format(double rawValue) => rawValue.ToString("N2", CultureInfo.InvariantCulture);
}
