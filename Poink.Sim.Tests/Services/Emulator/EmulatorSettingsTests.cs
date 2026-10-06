using Poink.Sim.Emulator;
using Xunit;

namespace Poink.Sim.Tests.Services.Emulator;

public class EmulatorSettingsTests
{
	private static readonly EmulatorSettings Settings = new();

	[Fact]
	public void RequiresReopen_SpeedAndLeverageDoNot()
	{
		Assert.False(Settings.RequiresReopen(Settings with { SpeedMultiplier = 1_000, Leverage = 20 }));
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
		Assert.True(Settings.RequiresReopen(Settings with { BrokerFund = 5 }));
	}
}
