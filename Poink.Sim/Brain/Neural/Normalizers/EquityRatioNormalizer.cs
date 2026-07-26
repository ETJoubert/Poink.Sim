// ── Normalizer: raw amount <-> ratio-over-equity ─────────────────────────
//  Expresses a raw money amount (e.g. unrealized/realized PnL, cash) as a
//  fraction of equity, keeping it bounded and scale-free regardless of
//  starting cash -- available for injection into any encoder constructor
//  that accepts an IFeatureNormalizer (e.g. TraderStateEncoder's
//  valueNormalizer parameter) when scale-free ratios are wanted instead of
//  exact amounts. Equity == 0 normalizes to 0 rather than dividing by zero.
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
