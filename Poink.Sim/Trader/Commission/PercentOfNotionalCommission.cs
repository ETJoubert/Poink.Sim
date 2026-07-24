// ── Default commission model: a percent of the fill's notional value ────
namespace Poink.Sim.Trader.Commission;

internal sealed class PercentOfNotionalCommission(double ratePercent) : ICommissionModel
{
    // Zero-impact default -- TraderSettings.EffectiveCommissionModel falls back
    // to this so an unconfigured Trader behaves exactly as before commission existed.
    public static readonly PercentOfNotionalCommission None = new(0);

    public double RatePercent { get; } = ratePercent;

    public double CommissionFor(double size, double fillPrice) =>
        size * fillPrice * (RatePercent / 100.0);
}
