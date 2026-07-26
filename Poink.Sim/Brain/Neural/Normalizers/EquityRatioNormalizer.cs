// ── Normalizer: raw amount <-> ratio-over-equity ─────────────────────────
//  Used by CapitalStateEncoder's unrealized/realized/cash-over-equity
//  features -- expressing each as a fraction of equity keeps them bounded
//  and scale-free regardless of starting cash. Equity == 0 normalizes to 0
//  rather than dividing by zero, same neutral convention as before this was
//  pulled out.
// ─────────────────────────────────────────────────────────────────────────
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
