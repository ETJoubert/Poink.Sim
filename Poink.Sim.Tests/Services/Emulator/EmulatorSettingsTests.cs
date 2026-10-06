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
