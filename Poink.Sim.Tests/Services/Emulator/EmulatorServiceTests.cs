using System;
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
		ActivityChance = 1,
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
		Assert.Equal(population.StartingPoints + service.Snapshot.Broker.AbsorbedLosses, population.Equity, 6);
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
