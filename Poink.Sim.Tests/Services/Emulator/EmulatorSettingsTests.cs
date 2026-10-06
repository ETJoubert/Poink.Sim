using Poink.Sim.Emulator;
using Xunit;

namespace Poink.Sim.Tests.Services.Emulator;

public class EmulatorSettingsTests
{
	private static readonly EmulatorSettings Settings = new();

	[Fact]
	public void RequiresReopen_LiveSettingsDoNot()
	{
		Assert.False(
			Settings.RequiresReopen(
				Settings with
				{
					SpeedMultiplier = 1_000,
					Leverage = 20,
					BrokerFund = 5,
					OrderRangeTicks = 5,
					ActivityChanceMin = 0.3,
					ActivityChanceMax = 0.9,
				}
			)
		);
	}

	[Fact]
	public void ActivityRangeDiffers_OnlyWhenTheRangeChanges()
	{
		Assert.False(Settings.ActivityRangeDiffers(Settings with { Leverage = 9 }));
		Assert.True(Settings.ActivityRangeDiffers(Settings with { ActivityChanceMax = 0.9 }));
	}

	[Fact]
	public void RequiresReopen_EqualPopulationsInANewListDoNot()
	{
		Assert.False(Settings.RequiresReopen(Settings with { Populations = [new PopulationSettings("Random", 200, 10_000)] }));
	}

	[Fact]
	public void RequiresReopen_GroupChancesDoNot()
	{
		Assert.False(Settings.RequiresReopen(Settings with { Populations = [new PopulationSettings("Random", 200, 10_000, 0.2, 0.3, 0.4)] }));
	}

	[Fact]
	public void RequiresReopen_AddingAGroupMoreBotsRenamingOrNewCapitalDoNot()
	{
		Assert.False(
			Settings.RequiresReopen(
				Settings with { Populations = [new PopulationSettings("Renamed", 300, 99), new PopulationSettings("Group 2", 20, 10_000)] }
			)
		);
	}

	[Fact]
	public void RequiresReopen_RemovingAGroupDoes()
	{
		var twoGroups = Settings with { Populations = [new PopulationSettings("A", 10, 100), new PopulationSettings("B", 10, 100)] };

		Assert.True(twoGroups.RequiresReopen(twoGroups with { Populations = [new PopulationSettings("A", 10, 100)] }));
	}

	[Theory]
	[InlineData(3, 3, 1, 0, true)]
	[InlineData(0, 0, 0, 0, true)]
	[InlineData(60, 50, 0, 0, true)]
	[InlineData(-1, 1, 0, 0, false)]
	[InlineData(double.NaN, 1, 0, 0, false)]
	public void HasValidWeights_AnyNonNegativeNumbers(double buy, double sell, double close, double hold, bool valid)
	{
		Assert.Equal(valid, new PopulationSettings("G", 1, 1, buy, sell, close, hold).HasValidWeights);
	}

	[Fact]
	public void Chances_AreEachWeightsShareOfTheTotal()
	{
		var group = new PopulationSettings("G", 1, 1, 3, 3, 1, 1);

		Assert.Equal((0.375, 0.375, 0.125, 0.125), (group.BuyChance, group.SellChance, group.CloseChance, group.HoldChance));
	}

	[Fact]
	public void Chances_AllZeroWeightsMeanTheGroupNeverActs()
	{
		var group = new PopulationSettings("G", 1, 1, 0, 0, 0, 0);

		Assert.Equal((0.0, 0.0, 0.0), (group.BuyChance, group.SellChance, group.CloseChance));
	}

	[Fact]
	public void RequiresReopen_ChangedPopulationsDo()
	{
		Assert.True(Settings.RequiresReopen(Settings with { Populations = [new PopulationSettings("Random", 50, 10_000)] }));
	}

	[Fact]
	public void RequiresReopen_OtherSettingsDo()
	{
		Assert.True(Settings.RequiresReopen(Settings with { StopOutLevelPercent = 80 }));
	}
}
