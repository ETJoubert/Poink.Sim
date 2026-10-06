using System;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Poink.Sim.Emulator;
using Poink.Sim.Emulator.Bots;
using Xunit;

namespace Poink.Sim.Tests.Services.Emulator;

public class EmulatorServiceTests
{
	private static readonly EmulatorSettings Settings = new()
	{
		StartPrice = 100,
		TickSize = 1,
		Populations = [new PopulationSettings("Test", 2, 10_000)],
		ActivityChanceMin = 1,
		ActivityChanceMax = 1,
		WarmupSteps = 0,
		HistoryCapacity = 8,
	};

	private static EmulatorService CreateService(EmulatorSettings? settings = null) =>
		new(new UpTickStrategy(), NullLogger<EmulatorService>.Instance, settings ?? Settings, TimeProvider.System, new Random(1));

	[Fact]
	public void Constructor_OpensTheMarketAtTheStartPrice()
	{
		using var service = CreateService();

		var snapshot = service.Snapshot;

		Assert.Equal(100, snapshot.Price);
		Assert.Equal(0, snapshot.Step);
		Assert.Single(snapshot.History);
		Assert.Equal(2, Assert.Single(snapshot.Populations).BotCount);
		Assert.Equal(Settings.BrokerFund, snapshot.Broker.FundBalance);
	}

	[Fact]
	public void Constructor_WarmUpTradesAndRecordsHistory()
	{
		using var service = CreateService(Settings with { WarmupSteps = 4 });

		Assert.Equal(5, service.Snapshot.History.Count);
		Assert.Equal(104, service.Snapshot.Price);
		Assert.Equal(4, service.Snapshot.Broker.TradeCount);
	}

	[Fact]
	public void Step_FillsMoveThePrice()
	{
		using var service = CreateService();

		service.Step();

		var snapshot = service.Snapshot;
		Assert.Equal(1, snapshot.Step);
		Assert.Equal(101, snapshot.Price);
		Assert.Equal(101, snapshot.History[^1].Price);
		Assert.Equal(1, snapshot.Broker.TradeCount);
	}

	[Fact]
	public void Step_CapsHistory()
	{
		using var service = CreateService();

		for (var i = 0; i < 20; i++)
		{
			service.Step();
		}

		Assert.Equal(Settings.HistoryCapacity, service.Snapshot.History.Count);
	}

	[Fact]
	public void Step_RaisesChanged()
	{
		using var service = CreateService();
		var raised = 0;
		service.Changed += () => raised++;

		service.Step();

		Assert.Equal(1, raised);
	}

	[Fact]
	public void Reset_ReopensTheMarket()
	{
		using var service = CreateService();
		service.Step();
		service.Step();

		service.Reset();

		Assert.Equal(0, service.Snapshot.Step);
		Assert.Equal(100, service.Snapshot.Price);
		Assert.Equal(0, service.Snapshot.Broker.TradeCount);
	}

	[Fact]
	public void RandomBots_TradeAZeroSumMarket()
	{
		var settings = new EmulatorSettings { WarmupSteps = 50 };
		using var service = new EmulatorService(
			new RandomBotStrategy(new Random(3)),
			NullLogger<EmulatorService>.Instance,
			settings,
			TimeProvider.System,
			new Random(3)
		);

		for (var i = 0; i < 100; i++)
		{
			service.Step();
		}

		var population = Assert.Single(service.Snapshot.Populations);
		Assert.True(service.Snapshot.Broker.TradeCount > 0);
		Assert.Equal(population.LongUnits, population.ShortUnits);
		Assert.Equal(population.Capital + service.Snapshot.Broker.AbsorbedLosses, population.Equity, 6);
	}

	[Fact]
	public void ApplySettings_FewerBotsReopensTheMarket()
	{
		using var service = CreateService(Settings with { Populations = [new PopulationSettings("Test", 5, 10_000)] });
		service.Step();
		var changed = 0;
		service.Changed += () => changed++;

		service.ApplySettings(service.Settings with { Populations = [new PopulationSettings("Test", 2, 10_000)] });

		Assert.Equal(2, service.Snapshot.Bots.Count);
		Assert.Equal(0, service.Snapshot.Step);
		Assert.Equal(1, changed);
	}

	[Fact]
	public void ApplySettings_MoreBotsJoinTheRunningMarket()
	{
		using var service = CreateService();
		service.Step();

		service.ApplySettings(service.Settings with { Populations = [new PopulationSettings("Test", 5, 300)] });

		Assert.Equal(1, service.Snapshot.Step);
		Assert.Equal([1, 2, 3, 4, 5], service.Snapshot.Bots.Select(bot => bot.Id));
		Assert.Equal(300, service.Snapshot.Bots[^1].Capital);
		Assert.Equal(10_000, service.Snapshot.Bots[0].Capital);
	}

	[Fact]
	public void ApplySettings_RenamingAGroupKeepsItsBots()
	{
		using var service = CreateService();
		service.Step();

		service.ApplySettings(service.Settings with { Populations = [new PopulationSettings("Renamed", 2, 10_000)] });

		Assert.Equal(1, service.Snapshot.Step);
		Assert.All(service.Snapshot.Bots, bot => Assert.Equal("Renamed", bot.Population));
		Assert.Equal("Renamed", Assert.Single(service.Snapshot.Populations).Name);
	}

	[Fact]
	public void Advance_RunsTheWholeBatchAsOneHistoryPoint()
	{
		using var service = CreateService();

		service.Advance(10);

		Assert.Equal(10, service.Snapshot.Step);
		Assert.Equal(110, service.Snapshot.Price);
		Assert.Equal(2, service.Snapshot.History.Count);
		Assert.Equal(10, service.Snapshot.Broker.TradeCount);
	}

	[Fact]
	public void ApplySettings_SpeedAndLeverageApplyToTheRunningMarket()
	{
		using var service = CreateService();
		service.Step();
		service.Step();

		service.ApplySettings(service.Settings with { SpeedMultiplier = 100, Leverage = 5 });

		Assert.Equal(100, service.Settings.SpeedMultiplier);
		Assert.Equal(5, service.Snapshot.Broker.Leverage);
		Assert.Equal(2, service.Snapshot.Step);
		Assert.Equal(2, service.Snapshot.Broker.TradeCount);
	}

	[Fact]
	public void SetSpeed_ChangesPlaybackWithoutTouchingTheMarket()
	{
		using var service = CreateService();
		service.Step();
		var changed = 0;
		service.Changed += () => changed++;

		service.SetSpeed(1_000);

		Assert.Equal(1_000, service.Settings.SpeedMultiplier);
		Assert.Equal(1, service.Snapshot.Step);
		Assert.Equal(1, changed);
	}

	[Fact]
	public void SetSpeed_RejectsSpeedsOutsideThePlaybackSteps()
	{
		using var service = CreateService();

		Assert.Throws<ArgumentOutOfRangeException>(() => service.SetSpeed(5));
		Assert.Equal(1, service.Settings.SpeedMultiplier);
	}

	[Fact]
	public void Advance_StopsEarlyOnceTheBudgetIsSpent()
	{
		using var service = CreateService();

		var done = service.Advance(1_000_000, TimeSpan.Zero);

		Assert.Equal(1, done);
		Assert.Equal(1, service.Snapshot.Step);
	}

	[Fact]
	public void StepsPerSecond_IsZeroWhileStopped()
	{
		using var service = CreateService();
		service.Step();

		Assert.Equal(0, service.StepsPerSecond);
	}

	[Fact]
	public void Replenish_AddsCapitalWithoutReopeningTheMarket()
	{
		using var service = CreateService();
		service.Step();

		service.Replenish();

		var population = Assert.Single(service.Snapshot.Populations);
		Assert.Equal(40_000, population.Capital);
		Assert.Equal(40_000, population.Equity, 6);
		Assert.Equal(1, service.Snapshot.Step);
	}

	[Fact]
	public void Step_RecordsTheTicksOpenHighLowAndClose()
	{
		using var service = CreateService();

		service.Step();

		Assert.Equal(new EmulatorPricePoint(service.Snapshot.History[^1].Timestamp, 100, 101, 100, 101), service.Snapshot.History[^1]);
	}

	[Fact]
	public void Replenish_OneBotPaysOnlyThatBot()
	{
		using var service = CreateService();

		service.Replenish(2);

		var bots = service.Snapshot.Bots;
		Assert.Equal((10_000.0, 20_000.0), (bots[0].Capital, bots[1].Capital));
	}

	[Fact]
	public void Bots_ActOnAFrequencyDrawnFromTheRange()
	{
		using var service = CreateService(Settings with { Populations = [new PopulationSettings("Test", 50, 10_000)], ActivityChanceMin = 0.2, ActivityChanceMax = 0.4 });

		Assert.All(service.Snapshot.Bots, bot => Assert.InRange(bot.ActivityChance, 0.2, 0.4));
	}

	[Fact]
	public void ApplySettings_ANewFrequencyRangeRedrawsEveryBotWithoutReopening()
	{
		using var service = CreateService();
		service.Step();

		service.ApplySettings(service.Settings with { ActivityChanceMin = 0.5, ActivityChanceMax = 0.6, OrderRangeTicks = 3 });

		Assert.All(service.Snapshot.Bots, bot => Assert.InRange(bot.ActivityChance, 0.5, 0.6));
		Assert.Equal(1, service.Snapshot.Step);
		Assert.Equal(3, service.Settings.OrderRangeTicks);
	}

	[Fact]
	public void ReplenishFund_TopsUpTheFundWithoutTouchingTheBots()
	{
		using var service = CreateService(Settings with { BrokerFund = 1_000 });
		service.Step();

		service.ReplenishFund();

		Assert.Equal((2_000.0, 2_000.0), (service.Snapshot.Broker.FundBalance, service.Snapshot.Broker.FundCapital));
		Assert.Equal(20_000, Assert.Single(service.Snapshot.Populations).Capital);
		Assert.Equal(1, service.Snapshot.Step);
	}

	[Fact]
	public void ApplySettings_ANewBrokerFundAppliesWithoutChangingTheBalance()
	{
		using var service = CreateService();
		service.Step();

		service.ApplySettings(service.Settings with { BrokerFund = 50 });

		Assert.Equal(Settings.BrokerFund, service.Snapshot.Broker.FundBalance);
		Assert.Equal(1, service.Snapshot.Step);
		service.ReplenishFund();
		Assert.Equal(Settings.BrokerFund + 50, service.Snapshot.Broker.FundBalance);
	}

	[Fact]
	public void ApplySettings_GroupChancesApplyWithoutReopening()
	{
		using var service = CreateService();
		service.Step();

		service.ApplySettings(service.Settings with { Populations = [new PopulationSettings("Test", 2, 10_000, 0, 0, 0)] });

		Assert.Equal(1, service.Snapshot.Step);
		var tradesBefore = service.Snapshot.Broker.TradeCount;
		service.Step();
		Assert.Equal(tradesBefore + 1, service.Snapshot.Broker.TradeCount);
	}

	[Fact]
	public void ApplySettings_AddingAGroupJoinsItsBotsToTheRunningMarket()
	{
		using var service = CreateService();
		service.Step();

		service.ApplySettings(
			service.Settings with { Populations = [new PopulationSettings("Test", 2, 10_000), new PopulationSettings("Group 2", 3, 500, 0.4, 0.4, 0.2)] }
		);

		Assert.Equal(1, service.Snapshot.Step);
		Assert.Equal(5, service.Snapshot.Bots.Count);
		Assert.Equal(["Test", "Group 2"], service.Snapshot.Populations.Select(p => p.Name));
		Assert.All(service.Snapshot.Bots.Skip(2), bot => Assert.Equal(("Group 2", 500.0), (bot.Population, bot.Capital)));
	}

	[Fact]
	public void ApplySettings_RemovingAGroupReopensTheMarket()
	{
		using var service = CreateService(
			Settings with { Populations = [new PopulationSettings("Test", 2, 10_000), new PopulationSettings("Group 2", 3, 500)] }
		);
		service.Step();

		service.ApplySettings(service.Settings with { Populations = [new PopulationSettings("Test", 2, 10_000)] });

		Assert.Equal(0, service.Snapshot.Step);
		Assert.Equal(2, service.Snapshot.Bots.Count);
	}

	[Fact]
	public void StartAndStop_ToggleIsRunning()
	{
		using var service = CreateService();

		service.Start();
		Assert.True(service.IsRunning);

		service.Stop();
		Assert.False(service.IsRunning);
	}

	private sealed class UpTickStrategy : IEmulatorBotStrategy
	{
		private bool _sellNext = true;

		public BotDecision Decide(BotMarketView view, EmulatorSettings settings)
		{
			var side = _sellNext ? OrderSide.Sell : OrderSide.Buy;
			_sellNext = !_sellNext;
			return new PlaceOrderDecision(side, view.LastPrice + view.TickSize, 1);
		}
	}
}
