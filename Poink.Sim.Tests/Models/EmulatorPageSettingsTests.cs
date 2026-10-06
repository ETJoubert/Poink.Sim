using System.Text.Json;
using Poink.Sim.Emulator;
using Poink.Sim.Models;
using Xunit;

namespace Poink.Sim.Tests.Models;

public class EmulatorPageSettingsTests
{
	[Fact]
	public void RoundTripsThroughJsonAndRestoresTheChoices()
	{
		var chosen = new EmulatorSettings
		{
			Populations = [new PopulationSettings("Random", 40, 2_500)],
			Leverage = 7.5,
			SpeedMultiplier = 100,
		};

		var json = JsonSerializer.Serialize(EmulatorPageSettings.From(chosen));
		var restored = JsonSerializer.Deserialize<EmulatorPageSettings>(json)!.ApplyTo(new EmulatorSettings());

		Assert.Equal(new PopulationSettings("Random", 40, 2_500), Assert.Single(restored.Populations));
		Assert.Equal((7.5, 100), (restored.Leverage, restored.SpeedMultiplier));
	}

	[Fact]
	public void ApplyTo_KeepsTheCurrentValueForAnythingNoLongerValid()
	{
		var current = new EmulatorSettings();
		var saved = new EmulatorPageSettings([new PopulationSettings("Random", 0, 100)], 500, 7);

		var applied = saved.ApplyTo(current);

		Assert.Same(current.Populations, applied.Populations);
		Assert.Equal((current.Leverage, current.SpeedMultiplier), (applied.Leverage, applied.SpeedMultiplier));
	}

	[Fact]
	public void ApplyTo_LeavesInternalTimingsAlone()
	{
		var current = new EmulatorSettings();

		var applied = EmulatorPageSettings.From(current with { Leverage = 3 }).ApplyTo(current);

		Assert.Equal(current.TickInterval, applied.TickInterval);
		Assert.Equal(current.HistoryCapacity, applied.HistoryCapacity);
		Assert.Equal(3, applied.Leverage);
	}
}
