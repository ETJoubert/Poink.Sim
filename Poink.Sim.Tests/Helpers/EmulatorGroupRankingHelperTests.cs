using Poink.Sim.Emulator;
using Poink.Sim.Helpers;
using Xunit;

namespace Poink.Sim.Tests.Helpers;

public class EmulatorGroupRankingHelperTests
{
	private static PopulationSummary Group(string name, double capital, double equity) =>
		new(name, 10, 10, capital, equity, equity, 0, 0, 0, 0, 0);

	[Fact]
	public void Medals_GoToTheThreeBestReturnsNotTheBiggestProfits()
	{
		PopulationSummary[] groups =
		[
			Group("Big", 1_000_000, 1_050_000),
			Group("Small", 1_000, 1_200),
			Group("Mid", 10_000, 10_900),
			Group("Loser", 10_000, 9_000),
		];

		var medals = EmulatorGroupRankingHelper.Medals(groups);

		Assert.Equal(EmulatorMedal.Gold, medals["Small"]);
		Assert.Equal(EmulatorMedal.Silver, medals["Mid"]);
		Assert.Equal(EmulatorMedal.Bronze, medals["Big"]);
		Assert.False(medals.ContainsKey("Loser"));
	}

	[Fact]
	public void Medals_TiesGoToTheNameThatSortsFirst()
	{
		var medals = EmulatorGroupRankingHelper.Medals([Group("B", 100, 110), Group("A", 100, 110)]);

		Assert.Equal((EmulatorMedal.Gold, EmulatorMedal.Silver), (medals["A"], medals["B"]));
	}

	[Fact]
	public void Medals_ALoneGroupWinsNothing()
	{
		Assert.Empty(EmulatorGroupRankingHelper.Medals([Group("Only", 100, 150)]));
	}

	[Fact]
	public void Return_IsProfitLossOverCapitalAndZeroWithoutCapital()
	{
		Assert.Equal(0.25, Group("G", 1_000, 1_250).Return, 9);
		Assert.Equal(0, Group("G", 0, 50).Return);
	}
}
