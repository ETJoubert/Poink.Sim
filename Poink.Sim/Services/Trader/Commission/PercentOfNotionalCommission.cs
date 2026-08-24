namespace Poink.Sim.Trader.Commission;

internal sealed class PercentOfNotionalCommission(double ratePercent) : ICommissionModel
{
	public static readonly PercentOfNotionalCommission None = new(0);

	public double RatePercent { get; } = ratePercent;

	public double CommissionFor(double size, double fillPrice) => size * fillPrice * (RatePercent / 100.0);
}
