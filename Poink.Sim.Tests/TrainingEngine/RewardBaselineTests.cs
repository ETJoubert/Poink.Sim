using Poink.Sim.Services.TrainingEngine;
using Xunit;

namespace Poink.Sim.Tests.TrainingEngine;

public class RewardBaselineTests
{
	[Fact]
	public void Advantage_BeforeTwoSamples_IsZero()
	{
		var baseline = new RewardBaseline();
		Assert.Equal(0, baseline.Advantage(5, 10));

		baseline.Add(1);

		Assert.Equal(0, baseline.Advantage(5, 10));
	}

	[Fact]
	public void Advantage_IsZScoreAgainstRunningMeanAndVariance()
	{
		var baseline = new RewardBaseline();
		baseline.Add(1);
		baseline.Add(3);

		double mean = 1 + 0.01 * 2;
		double std = System.Math.Sqrt(0.99 * 0.01 * 4);

		Assert.Equal((2 - mean) / std, baseline.Advantage(2, 100), 4);
		Assert.True(baseline.Advantage(0, 100) < 0);
	}

	[Theory]
	[InlineData(1000, 10)]
	[InlineData(-1000, -10)]
	public void Advantage_IsClampedToMaxMagnitude(double reward, double expected)
	{
		var baseline = new RewardBaseline();
		baseline.Add(1);
		baseline.Add(3);

		Assert.Equal(expected, baseline.Advantage(reward, 10));
	}

	[Fact]
	public void Advantage_ZeroVarianceHistory_SaturatesInsteadOfDividingByZero()
	{
		var baseline = new RewardBaseline();
		baseline.Add(0);
		baseline.Add(0);

		Assert.Equal(10, baseline.Advantage(1, 10));
		Assert.Equal(0, baseline.Advantage(0, 10));
	}
}
