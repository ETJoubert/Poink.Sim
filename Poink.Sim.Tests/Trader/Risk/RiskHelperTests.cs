using Poink.Sim.Models;
using Poink.Sim.Tests.TestSupport;
using Poink.Sim.Trader.Risk;
using Xunit;

namespace Poink.Sim.Tests.Trader.Risk;

public class RiskHelperTests
{
	private static PositionDirection Direction(bool isLong) => isLong ? PositionDirection.Long : PositionDirection.Short;

	[Theory]
	[InlineData(true, 10, 100, 95, 50)]
	[InlineData(false, 10, 100, 105, 50)]
	[InlineData(true, 10, 100, 105, -50)]
	public void RiskOnStop_IsSizeTimesAdverseMove(bool isLong, double size, double entry, double stop, double expected)
	{
		Assert.Equal(expected, RiskHelper.RiskOnStop(Direction(isLong), size, entry, stop), 9);
	}

	[Theory]
	[InlineData(true, 10, 100, 110, 100)]
	[InlineData(false, 10, 100, 90, 100)]
	[InlineData(false, 10, 100, 110, -100)]
	public void RewardOnTarget_IsSizeTimesFavourableMove(bool isLong, double size, double entry, double target, double expected)
	{
		Assert.Equal(expected, RiskHelper.RewardOnTarget(Direction(isLong), size, entry, target), 9);
	}

	[Theory]
	[InlineData(1000, 0.5, 10, 50, 100)]
	[InlineData(1000, 2, 1, 100, 10)]
	[InlineData(1000, -1, 1, 100, 0)]
	[InlineData(1000, 0.5, 10, 0, 0)]
	public void PositionSize_MirrorsTraderSizing(double cash, double fraction, double leverage, double entry, double expected)
	{
		Assert.Equal(expected, RiskHelper.PositionSize(cash, fraction, leverage, entry), 9);
	}

	[Theory]
	[InlineData(100, 50, 10, 500)]
	[InlineData(100, 50, 0, 0)]
	public void MarginRequired_DividesNotionalByLeverage(double size, double entry, double leverage, double expected)
	{
		Assert.Equal(expected, RiskHelper.MarginRequired(size, entry, leverage), 9);
	}

	[Theory]
	[InlineData(false, 1000, 0.5, 10, 50, 750)]
	[InlineData(true, 1000, 0.5, 10, 50, 750)]
	[InlineData(true, 1000, 0.1, 1, 50, 100)]
	public void MaxRiskAtStopOut_IsStopOutFloorCappedByLongNotional(
		bool isLong,
		double cash,
		double fraction,
		double leverage,
		double stopOutPercent,
		double expected
	)
	{
		Assert.Equal(expected, RiskHelper.MaxRiskAtStopOut(Direction(isLong), cash, fraction, leverage, stopOutPercent), 9);
	}

	[Theory]
	[InlineData(50, 100, 95, 10)]
	[InlineData(50, 95, 100, 10)]
	[InlineData(50, 100, 100, 0)]
	public void SizeForRisk_DividesRiskByStopDistance(double risk, double entry, double stop, double expected)
	{
		Assert.Equal(expected, RiskHelper.SizeForRisk(risk, entry, stop), 9);
	}

	[Fact]
	public void SizeForRiskRounded_RoundsToVolumeStep()
	{
		Assert.Equal(16.67, RiskHelper.SizeForRiskRounded(50, 100, 97, 0.01)!.Value, 9);
	}

	[Theory]
	[InlineData(0.001, 100, 99, 1)]
	[InlineData(50, 100, 100, 0.01)]
	public void SizeForRiskRounded_ReturnsNullWithoutUsableSize(double risk, double entry, double stop, double step)
	{
		Assert.Null(RiskHelper.SizeForRiskRounded(risk, entry, stop, step));
	}

	[Theory]
	[InlineData(1.234, 0.01, 1.23)]
	[InlineData(0.30000000000000004, 0.1, 0.3)]
	[InlineData(1.234, 0, 1.234)]
	[InlineData(1.234, -1, 1.234)]
	public void RoundToStep_SnapsToNearestMultipleWithoutResidue(double value, double step, double expected)
	{
		Assert.Equal(expected, RiskHelper.RoundToStep(value, step));
	}

	[Theory]
	[InlineData(50, 1000, 10, 100, 95, 0.1)]
	[InlineData(50, 0, 10, 100, 95, 0)]
	[InlineData(50, 1000, 0, 100, 95, 0)]
	public void CapitalFractionForRisk_InvertsProjectedRisk(
		double risk,
		double cash,
		double leverage,
		double entry,
		double stop,
		double expected
	)
	{
		Assert.Equal(expected, RiskHelper.CapitalFractionForRisk(risk, cash, leverage, entry, stop), 9);
	}

	[Fact]
	public void CapitalFractionForRisk_RoundTripsThroughProjectedRisk()
	{
		double fraction = RiskHelper.CapitalFractionForRisk(37.5, 2500, 30, 1.0850, 1.0790);

		double risk = RiskHelper.ProjectedRiskOnStop(PositionDirection.Long, 2500, fraction, 30, 1.0850, 1.0790);

		Assert.Equal(37.5, risk, 6);
	}

	[Fact]
	public void CapitalFractionForRiskBounded_ReturnsNullWhenMoreThanAllCashIsNeeded()
	{
		Assert.Null(RiskHelper.CapitalFractionForRiskBounded(5000, 1000, 1, 100, 99));
		Assert.Equal(0.1, RiskHelper.CapitalFractionForRiskBounded(50, 1000, 10, 100, 95)!.Value, 9);
	}

	[Theory]
	[InlineData(true, 1.1000, 20, 0.0001, 1.0980, 1.1020)]
	[InlineData(false, 1.1000, 20, 0.0001, 1.1020, 1.0980)]
	public void PipPrices_PlaceStopOnLosingSideAndTargetOnWinningSide(
		bool isLong,
		double entry,
		double pips,
		double pipSize,
		double expectedStop,
		double expectedTarget
	)
	{
		Assert.Equal(expectedStop, RiskHelper.StopLossPriceFromPips(Direction(isLong), entry, pips, pipSize), 9);
		Assert.Equal(expectedTarget, RiskHelper.TakeProfitPriceFromPips(Direction(isLong), entry, pips, pipSize), 9);
	}

	[Fact]
	public void StopLossPriceForSizeRisk_PlacesStopThatLosesExactlyTheRisk()
	{
		Assert.Equal(95, RiskHelper.StopLossPriceForSizeRisk(PositionDirection.Long, 10, 100, 50)!.Value, 9);
		Assert.Equal(105, RiskHelper.StopLossPriceForSizeRisk(PositionDirection.Short, 10, 100, 50)!.Value, 9);
		Assert.Null(RiskHelper.StopLossPriceForSizeRisk(PositionDirection.Long, 0, 100, 50));
	}

	[Fact]
	public void TakeProfitPriceForReward_PlacesTargetThatGainsExactlyTheReward()
	{
		Assert.Equal(95, RiskHelper.TakeProfitPriceForSizeReward(PositionDirection.Short, 10, 100, 50)!.Value, 9);
		Assert.Equal(105, RiskHelper.TakeProfitPriceForReward(PositionDirection.Long, 1000, 1, 1, 100, 50)!.Value, 9);
		Assert.Null(RiskHelper.TakeProfitPriceForReward(PositionDirection.Long, 1000, 0, 1, 100, 50));
	}

	[Fact]
	public void Ratios_ReturnZeroOrNullWhenUndefined()
	{
		Assert.Equal(0.05, RiskHelper.RiskFraction(50, 1000), 9);
		Assert.Equal(0, RiskHelper.RiskFraction(50, 0));
		Assert.Equal(5, RiskHelper.PercentOfBalance(50, 1000)!.Value, 9);
		Assert.Null(RiskHelper.PercentOfBalance(null, 1000));
		Assert.Null(RiskHelper.PercentOfBalance(50, 0));
		Assert.Equal(2, RiskHelper.RewardToRiskRatio(50, 100)!.Value, 9);
		Assert.Null(RiskHelper.RewardToRiskRatio(0, 100));
		Assert.Null(RiskHelper.RewardToRiskRatio(50, -10));
	}

	[Fact]
	public void PriceDistanceInPips_IsAbsolute()
	{
		Assert.Equal(20, RiskHelper.PriceDistanceInPips(1.1020, 1.1000, 0.0001), 6);
		Assert.Equal(20, RiskHelper.PriceDistanceInPips(1.1000, 1.1020, 0.0001), 6);
	}

	[Theory]
	[InlineData(100, 99.5, 2, 98)]
	[InlineData(100, 101, 2, 102)]
	[InlineData(100, 97, 2, 97)]
	[InlineData(100, 99.5, 0, 99.5)]
	public void ClampToMinimumDistance_PushesShortDistancesOutOnTheSameSide(
		double entry,
		double price,
		double minDistance,
		double expected
	)
	{
		Assert.Equal(expected, RiskHelper.ClampToMinimumDistance(entry, price, minDistance), 9);
	}

	[Fact]
	public void PositionRiskOnStop_IsZeroWithoutStop()
	{
		var unprotected = new Position(PositionDirection.Long, 10, 100, TestTickHelper.Origin);
		var protectedPosition = unprotected with { StopLossPrice = 95 };

		Assert.Equal(0, unprotected.RiskOnStop());
		Assert.Equal(50, protectedPosition.RiskOnStop(), 9);
	}
}
