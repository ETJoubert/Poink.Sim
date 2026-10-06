using System.Linq;
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

		var json = JsonSerializer.Serialize(
			EmulatorPageSettings.From(
				chosen with { OrderRangeTicks = 7, ActivityChanceMin = 0.3, ActivityChanceMax = 0.6, BrokerFund = 250_000 },
				EmulatorChartMode.Ohlc
			)
		);
		var saved = JsonSerializer.Deserialize<EmulatorPageSettings>(json)!;
		var restored = saved.ApplyTo(new EmulatorSettings());

		Assert.Equal(new PopulationSettings("Random", 40, 2_500), Assert.Single(restored.Populations));
		Assert.Equal((7.5, 100, 7), (restored.Leverage, restored.SpeedMultiplier, restored.OrderRangeTicks));
		Assert.Equal((0.3, 0.6), (restored.ActivityChanceMin, restored.ActivityChanceMax));
		Assert.Equal(EmulatorChartMode.Ohlc, saved.ChartMode);
		Assert.Equal(250_000, restored.BrokerFund);
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
	public void SettingsSavedBeforeNewChoicesStillLoad()
	{
		var current = new EmulatorSettings();
		var json = """{"Populations":[{"Name":"Random","BotCount":30,"StartingPoints":500}],"Leverage":4,"SpeedMultiplier":10}""";

		var saved = JsonSerializer.Deserialize<EmulatorPageSettings>(json)!;
		var applied = saved.ApplyTo(current);

		Assert.Null(saved.ChartMode);
		Assert.Equal((4.0, 10, 30), (applied.Leverage, applied.SpeedMultiplier, applied.Populations[0].BotCount));
		Assert.Equal((current.OrderRangeTicks, current.ActivityChanceMin), (applied.OrderRangeTicks, applied.ActivityChanceMin));
		Assert.Equal(current.BrokerFund, applied.BrokerFund);
	}

	[Fact]
	public void GroupsSavedBeforeActionChancesLoadWithTheOldBehaviour()
	{
		var json = """{"Populations":[{"Name":"Random","BotCount":30,"StartingPoints":500}],"Leverage":4,"SpeedMultiplier":10}""";

		var group = Assert.Single(JsonSerializer.Deserialize<EmulatorPageSettings>(json)!.ApplyTo(new EmulatorSettings()).Populations);

		Assert.Equal((0.5, 0.5, 0.0), (group.BuyChance, group.SellChance, group.CloseChance));
	}

	[Fact]
	public void ApplyTo_RejectsGroupsWithInvalidChancesOrDuplicateNames()
	{
		var current = new EmulatorSettings();

		var overSpent = new EmulatorPageSettings([new PopulationSettings("A", 5, 100, 0.8, 0.8, 0)], 2, 1).ApplyTo(current);
		var duplicated = new EmulatorPageSettings([new PopulationSettings("A", 5, 100), new PopulationSettings("A", 5, 100)], 2, 1).ApplyTo(current);

		Assert.Same(current.Populations, overSpent.Populations);
		Assert.Same(current.Populations, duplicated.Populations);
	}

	[Fact]
	public void ApplyTo_RejectsMoreGroupsThanTheMaximum()
	{
		var current = new EmulatorSettings();
		var tooMany = Enumerable.Range(1, EmulatorSettings.MaxGroups + 1).Select(i => new PopulationSettings($"G{i}", 5, 100)).ToArray();

		var applied = new EmulatorPageSettings(tooMany, 2, 1).ApplyTo(current);

		Assert.Same(current.Populations, applied.Populations);
	}

	[Fact]
	public void ApplyTo_LeavesInternalTimingsAlone()
	{
		var current = new EmulatorSettings();

		var applied = EmulatorPageSettings.From(current with { Leverage = 3 }, EmulatorChartMode.Line).ApplyTo(current);

		Assert.Equal(current.TickInterval, applied.TickInterval);
		Assert.Equal(current.HistoryCapacity, applied.HistoryCapacity);
		Assert.Equal(3, applied.Leverage);
	}
}
