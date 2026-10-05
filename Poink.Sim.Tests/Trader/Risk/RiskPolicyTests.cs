using System.Linq;
using Poink.Sim.Models;
using Poink.Sim.Tests.TestSupport;
using Poink.Sim.Trader.Risk;
using Poink.Sim.Trader.Risk.Policies;
using Xunit;

namespace Poink.Sim.Tests.Trader.Risk;

public class RiskPolicyTests
{
	private static SimulationDecision Long(double fraction, double? stopLoss = null) =>
		new(SimulationAction.EnterLong, fraction, stopLoss);

	[Fact]
	public void MaxOpenRisk_NonEntryDecision_IsPermitted()
	{
		var review = new MaxOpenRiskPolicy().Review(new SimulationDecision(SimulationAction.Close), RiskContextHelper.Flat());

		Assert.Equal(RiskVerdict.Permit, review.Verdict);
	}

	[Fact]
	public void MaxOpenRisk_EntryWithoutStop_IsDenied()
	{
		var review = new MaxOpenRiskPolicy().Review(Long(0.1), RiskContextHelper.Flat());

		Assert.Equal(RiskVerdict.Deny, review.Verdict);
		Assert.Equal(MaxOpenRiskPolicy.PolicyName, review.PolicyName);
	}

	[Fact]
	public void MaxOpenRisk_EntryWithinCap_IsPermitted()
	{
		var review = new MaxOpenRiskPolicy().Review(Long(0.1, stopLoss: 99), RiskContextHelper.Flat());

		Assert.Equal(RiskVerdict.Permit, review.Verdict);
	}

	[Fact]
	public void MaxOpenRisk_EntryOverCap_IsShrunkToCap()
	{
		var context = RiskContextHelper.Flat();

		var review = new MaxOpenRiskPolicy().Review(Long(1, stopLoss: 95), context);

		Assert.Equal(RiskVerdict.Modify, review.Verdict);
		Assert.Equal(0.04, review.Decision.CapitalFraction, 9);
		double resultingRisk = RiskHelper.ProjectedRiskOnStop(
			PositionDirection.Long,
			context.Cash,
			review.Decision.CapitalFraction,
			context.Leverage,
			context.Ask,
			95
		);
		Assert.Equal(0.02 * context.Capital.Equity, resultingRisk, 9);
	}

	[Fact]
	public void PercentStopLoss_EntryWithoutStop_AttachesStopRiskingTwoPercentOfCash()
	{
		var review = new PercentStopLossPolicy().Review(Long(0.5), RiskContextHelper.Flat());

		Assert.Equal(RiskVerdict.Modify, review.Verdict);
		Assert.Equal(99.6, review.Decision.StopLossPrice!.Value, 9);
	}

	[Fact]
	public void PercentStopLoss_ShortEntry_PlacesStopAboveEntry()
	{
		var decision = new SimulationDecision(SimulationAction.EnterShort, 0.5);

		var review = new PercentStopLossPolicy().Review(decision, RiskContextHelper.Flat());

		Assert.Equal(100.4, review.Decision.StopLossPrice!.Value, 9);
	}

	[Fact]
	public void PercentStopLoss_EntryWithStop_IsLeftAlone()
	{
		var review = new PercentStopLossPolicy().Review(Long(0.5, stopLoss: 90), RiskContextHelper.Flat());

		Assert.Equal(RiskVerdict.Permit, review.Verdict);
		Assert.Equal(90, review.Decision.StopLossPrice);
	}

	[Fact]
	public void LossStreak_BelowThreshold_PermitsFullVolume()
	{
		var policy = new LossStreakVolumePolicy();
		policy.Supervise(RiskContextHelper.WithTrades([RiskContextHelper.ClosedTrade(1, -5), RiskContextHelper.ClosedTrade(2, -5)]));

		var review = policy.Review(Long(0.4), RiskContextHelper.Flat());

		Assert.Equal(RiskVerdict.Permit, review.Verdict);
		Assert.Equal("2/3 losses", policy.Status);
	}

	[Fact]
	public void LossStreak_AtThreshold_HalvesEntryVolume()
	{
		var policy = new LossStreakVolumePolicy();
		var trades = Enumerable.Range(1, 3).Select(id => RiskContextHelper.ClosedTrade(id, -5)).ToList();
		policy.Supervise(RiskContextHelper.WithTrades(trades));

		var review = policy.Review(Long(0.4), RiskContextHelper.Flat());

		Assert.Equal(RiskVerdict.Modify, review.Verdict);
		Assert.Equal(0.2, review.Decision.CapitalFraction, 9);
	}

	[Fact]
	public void LossStreak_WinningTrade_ResetsStreak()
	{
		var policy = new LossStreakVolumePolicy();
		var trades = Enumerable.Range(1, 3).Select(id => RiskContextHelper.ClosedTrade(id, -5)).ToList();
		policy.Supervise(RiskContextHelper.WithTrades(trades));
		trades.Add(RiskContextHelper.ClosedTrade(4, 10));
		policy.Supervise(RiskContextHelper.WithTrades(trades));

		var review = policy.Review(Long(0.4), RiskContextHelper.Flat());

		Assert.Equal(RiskVerdict.Permit, review.Verdict);
		Assert.Null(policy.Status);
	}

	[Fact]
	public void LossStreak_SameTradesSeenTwice_AreCountedOnce()
	{
		var policy = new LossStreakVolumePolicy();
		var context = RiskContextHelper.WithTrades([RiskContextHelper.ClosedTrade(1, -5), RiskContextHelper.ClosedTrade(2, -5)]);

		policy.Supervise(context);
		policy.Supervise(context);

		Assert.Equal("2/3 losses", policy.Status);
	}

	[Fact]
	public void ProfitGiveback_ClosesOnceGainFallsPastGivebackOfPeak()
	{
		var policy = new ProfitGivebackPolicy(30);
		var position = new Position(PositionDirection.Long, 10, 100, TestTickHelper.Origin);

		Assert.Empty(policy.Supervise(RiskContextHelper.WithPosition(position, 110, 110)));
		Assert.Empty(policy.Supervise(RiskContextHelper.WithPosition(position, 107.5, 107.5)));
		var actions = policy.Supervise(RiskContextHelper.WithPosition(position, 106, 106));

		Assert.Equal(SimulationAction.Close, Assert.Single(actions).Action);
	}

	[Fact]
	public void ProfitGiveback_NeverInProfit_DoesNothing()
	{
		var policy = new ProfitGivebackPolicy(30);
		var position = new Position(PositionDirection.Long, 10, 100, TestTickHelper.Origin);

		Assert.Empty(policy.Supervise(RiskContextHelper.WithPosition(position, 95, 95)));
		Assert.Null(policy.Status);
	}

	[Fact]
	public void ProfitGiveback_FlatContext_ResetsPeakForNextPosition()
	{
		var policy = new ProfitGivebackPolicy(30);
		var position = new Position(PositionDirection.Long, 10, 100, TestTickHelper.Origin);
		policy.Supervise(RiskContextHelper.WithPosition(position, 120, 120));

		policy.Supervise(RiskContextHelper.Flat());

		Assert.Null(policy.Status);
		Assert.Empty(policy.Supervise(RiskContextHelper.WithPosition(position, 105, 105)));
	}

	[Fact]
	public void ProfitGiveback_SetParameter_ChangesGiveback()
	{
		var policy = new ProfitGivebackPolicy(30);
		var parameter = Assert.Single(policy.Parameters);

		policy.SetParameter(parameter.Key, 50);

		Assert.Equal(50, Assert.Single(policy.Parameters).Value);
	}

	[Fact]
	public void Fresh_ReturnsIndependentInstance()
	{
		var policy = new LossStreakVolumePolicy();
		var trades = Enumerable.Range(1, 3).Select(id => RiskContextHelper.ClosedTrade(id, -5)).ToList();
		policy.Supervise(RiskContextHelper.WithTrades(trades));

		var fresh = policy.Fresh();

		Assert.NotSame(policy, fresh);
		Assert.Null(fresh.Status);
	}
}
