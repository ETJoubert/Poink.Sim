using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Poink.Sim.Emulator;
using Xunit;

namespace Poink.Sim.Tests.Services.Emulator;

public class EmulatorServiceTests
{
	private static readonly EmulatorSettings Settings = new()
	{
		StartPrice = 10,
		TickSize = 1,
		WarmupSteps = 4,
		HistoryCapacity = 8,
	};

	private static EmulatorService CreateService() =>
		new(new StepUpGenerator(), NullLogger<EmulatorService>.Instance, Settings, TimeProvider.System);

	[Fact]
	public void Constructor_WarmsUpHistoryFromStartPriceToCurrentPrice()
	{
		using var service = CreateService();

		var snapshot = service.Snapshot;

		Assert.Equal(Settings.WarmupSteps + 1, snapshot.History.Count);
		Assert.Equal(Settings.StartPrice, snapshot.History[0].Price);
		Assert.Equal(snapshot.Price, snapshot.History[^1].Price);
		Assert.Equal(14, snapshot.Price);
		Assert.Equal(0, snapshot.Step);
	}

	[Fact]
	public void Step_MovesPriceRebuildsBookAndCapsHistory()
	{
		using var service = CreateService();

		for (var i = 0; i < 10; i++)
		{
			service.Step();
		}

		var snapshot = service.Snapshot;
		Assert.Equal(10, snapshot.Step);
		Assert.Equal(24, snapshot.Price);
		Assert.Equal(Settings.HistoryCapacity, snapshot.History.Count);
		Assert.Equal(24, snapshot.History[^1].Price);
		Assert.Equal(23, snapshot.Book.BestBid);
		Assert.Equal(new PopulationLevel(23, 2, 100), Assert.Single(snapshot.Population));
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
	public void Reset_RestartsFromStartPrice()
	{
		using var service = CreateService();
		service.Step();
		service.Step();

		service.Reset();

		Assert.Equal(0, service.Snapshot.Step);
		Assert.Equal(14, service.Snapshot.Price);
		Assert.Equal(Settings.StartPrice, service.Snapshot.History[0].Price);
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

	private sealed class StepUpGenerator : ISyntheticMarketGenerator
	{
		public double NextPrice(double price, EmulatorSettings settings) => price + settings.TickSize;

		public OrderBook BuildBook(double price, EmulatorSettings settings) =>
			new([new OrderBookLevel(price - settings.TickSize, 1)], [new OrderBookLevel(price + settings.TickSize, 1)]);

		public IReadOnlyList<PopulationLevel> BuildPopulation(OrderBook book, EmulatorSettings settings) =>
			[new PopulationLevel(book.BestBid!.Value, 2, 100)];
	}
}
