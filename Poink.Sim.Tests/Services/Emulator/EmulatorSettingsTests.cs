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
	[InlineData(0.5, 0.5, 0, true)]
	[InlineData(0.4, 0.4, 0.2, true)]
	[InlineData(0.6, 0.5, 0, false)]
	[InlineData(-0.1, 0.5, 0, false)]
	public void HasValidActionChances_EachWithinRangeAndAtMostOneInTotal(double buy, double sell, double close, bool valid)
	{
		Assert.Equal(valid, new PopulationSettings("G", 1, 1, buy, sell, close).HasValidActionChances);
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
