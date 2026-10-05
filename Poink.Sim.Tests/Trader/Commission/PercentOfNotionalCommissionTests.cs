using Poink.Sim.Trader.Commission;
using Xunit;

namespace Poink.Sim.Tests.Trader.Commission;

public class PercentOfNotionalCommissionTests
{
	[Theory]
	[InlineData(0.5, 1000, 2, 10)]
	[InlineData(1, 40_000, 1.25, 500)]
	[InlineData(0, 1000, 2, 0)]
	public void CommissionFor_ChargesRateOfNotional(double ratePercent, double size, double fillPrice, double expected)
	{
		Assert.Equal(expected, new PercentOfNotionalCommission(ratePercent).CommissionFor(size, fillPrice), 9);
	}

	[Fact]
	public void None_ChargesNothing()
	{
		Assert.Equal(0, PercentOfNotionalCommission.None.CommissionFor(1_000_000, 1.5));
	}
}
