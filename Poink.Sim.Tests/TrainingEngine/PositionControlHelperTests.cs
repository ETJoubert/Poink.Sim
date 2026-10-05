using Poink.Sim.Models;
using Poink.Sim.Models.TrainingModels;
using Poink.Sim.Services.TrainingEngine;
using Xunit;

namespace Poink.Sim.Tests.TrainingEngine;

public class PositionControlHelperTests
{
	private const double PipSize = 0.0001;

	private static readonly PositionControlOptionsModel FixedWithProtection = new(
		PositionSizingMode.FixedVolume,
		Volume: 1000,
		StopLoss: 20,
		StopLossUnit: StopLossUnit.Pips,
		TakeProfitBalancePercent: 3
	);

	[Fact]
	public void Preview_FixedVolumeWithinMargin_UsesRequestedVolume()
	{
		var preview = PositionControlHelper.Preview(new(Volume: 1000), 10_000, 1, 2, PipSize);

		Assert.Equal(1000, preview.Volume);
		Assert.Equal(2000, preview.Notional);
		Assert.Equal(2000, preview.Margin);
		Assert.Equal(0, preview.LossAtStop);
		Assert.False(preview.MarginLimited);
	}

	[Fact]
	public void Preview_FixedVolumeBeyondMargin_IsCutToAffordableVolume()
	{
		var preview = PositionControlHelper.Preview(new(Volume: 1e9), 10_000, 1, 2, PipSize);

		Assert.Equal(5000, preview.Volume);
		Assert.True(preview.MarginLimited);
	}

	[Fact]
	public void Preview_RiskPerTrade_SizesSoStopLosesRiskPercent()
	{
		var control = new PositionControlOptionsModel(PositionSizingMode.RiskPerTrade, RiskPerTradePercent: 1, StopLoss: 20);

		var preview = PositionControlHelper.Preview(control, 10_000, 100, 1.0, PipSize);

		Assert.Equal(50_000, preview.Volume, 6);
		Assert.Equal(100, preview.LossAtStop, 6);
		Assert.Equal(0.002, preview.StopDistance, 9);
		Assert.Equal(500, preview.Margin, 6);
	}

	[Fact]
	public void Preview_RiskPerTradeWithoutStop_HasNoVolume()
	{
		var control = new PositionControlOptionsModel(PositionSizingMode.RiskPerTrade, StopLoss: 0);

		Assert.Equal(0, PositionControlHelper.Preview(control, 10_000, 100, 1.0, PipSize).Volume);
	}

	[Fact]
	public void Preview_BalancePercentStopAndTarget_DeriveDistancesFromVolume()
	{
		var control = new PositionControlOptionsModel(
			Volume: 1000,
			StopLoss: 2,
			StopLossUnit: StopLossUnit.BalancePercent,
			TakeProfitBalancePercent: 3
		);

		var preview = PositionControlHelper.Preview(control, 10_000, 1, 2, PipSize);

		Assert.Equal(200, preview.LossAtStop, 9);
		Assert.Equal(0.2, preview.StopDistance, 9);
		Assert.Equal(300, preview.ProfitAtTarget, 9);
		Assert.Equal(0.3, preview.TakeProfitDistance, 9);
		Assert.Equal(1.5, preview.RewardRisk!.Value, 9);
	}

	[Theory]
	[InlineData(0, 10_000, PipSize)]
	[InlineData(2, 0, PipSize)]
	[InlineData(2, 10_000, 0)]
	public void Preview_InvalidInputs_ReturnsEmptyPreview(double price, double cash, double pipSize)
	{
		Assert.Equal(0, PositionControlHelper.Preview(new(Volume: 1000), cash, 1, price, pipSize).Volume);
	}

	[Theory]
	[InlineData(SimulationAction.Hold)]
	[InlineData(SimulationAction.Close)]
	public void BuildDecision_NonEntry_PassesThrough(SimulationAction action)
	{
		var decision = PositionControlHelper.BuildDecision(action, FixedWithProtection, 10_000, 1, PipSize, 2, 2);

		Assert.Equal(action, decision.Action);
		Assert.Equal(0, decision.CapitalFraction);
	}

	[Fact]
	public void BuildDecision_Long_PlacesStopBelowAndTargetAboveAsk()
	{
		var decision = PositionControlHelper.BuildDecision(SimulationAction.EnterLong, FixedWithProtection, 10_000, 1, PipSize, 1.9998, 2.0);

		Assert.Equal(SimulationAction.EnterLong, decision.Action);
		Assert.Equal(0.2, decision.CapitalFraction, 9);
		Assert.Equal(1.998, decision.StopLossPrice!.Value, 9);
		Assert.Equal(2.3, decision.TakeProfitPrice!.Value, 9);
	}

	[Fact]
	public void BuildDecision_Short_PlacesStopAboveAndTargetBelowBid()
	{
		var decision = PositionControlHelper.BuildDecision(SimulationAction.EnterShort, FixedWithProtection, 10_000, 1, PipSize, 2.0, 2.0002);

		Assert.Equal(2.002, decision.StopLossPrice!.Value, 9);
		Assert.Equal(1.7, decision.TakeProfitPrice!.Value, 9);
	}

	[Fact]
	public void BuildDecision_NoAffordableVolume_DowngradesToHold()
	{
		var decision = PositionControlHelper.BuildDecision(SimulationAction.EnterLong, FixedWithProtection, 0, 1, PipSize, 2, 2);

		Assert.Equal(SimulationAction.Hold, decision.Action);
	}

	[Fact]
	public void BuildDecision_WithoutProtection_LeavesStopAndTargetUnset()
	{
		var decision = PositionControlHelper.BuildDecision(SimulationAction.EnterLong, new(Volume: 10), 10_000, 1, PipSize, 2, 2);

		Assert.Null(decision.StopLossPrice);
		Assert.Null(decision.TakeProfitPrice);
	}
}
