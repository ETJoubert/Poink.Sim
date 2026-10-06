using System;
using System.Linq;
using Poink.Sim.Emulator;
using Xunit;

namespace Poink.Sim.Tests.Services.Emulator;

public class SyntheticMarketGeneratorTests
{
	private static readonly EmulatorSettings Settings = new();

	private static SyntheticMarketGenerator CreateGenerator() => new(new Random(42));

	[Fact]
	public void BuildBook_PlacesBidsBelowAsksSeparatedBySpread()
	{
		var book = CreateGenerator().BuildBook(100, Settings);

		Assert.Equal(Settings.BookDepth, book.Bids.Count);
		Assert.Equal(Settings.BookDepth, book.Asks.Count);
		Assert.True(book.BestBid < 100 && book.BestAsk > 100);
		Assert.Equal(Settings.SpreadTicks * Settings.TickSize, book.Spread!.Value, 9);
	}

	[Fact]
	public void BuildBook_OrdersEachSideBestPriceFirstWithPositiveVolume()
	{
		var book = CreateGenerator().BuildBook(100, Settings);

		Assert.Equal(book.Bids.OrderByDescending(l => l.Price), book.Bids);
		Assert.Equal(book.Asks.OrderBy(l => l.Price), book.Asks);
		Assert.All(book.Bids.Concat(book.Asks), level => Assert.True(level.Volume >= 1));
	}

	[Fact]
	public void BuildBook_DropsBidLevelsAtOrBelowZero()
	{
		var book = CreateGenerator().BuildBook(0.05, Settings);

		Assert.All(book.Bids, level => Assert.True(level.Price > 0));
		Assert.Equal(Settings.BookDepth, book.Asks.Count);
	}

	[Fact]
	public void BuildPopulation_PlacesBotsWithBalanceOnEveryBookLevel()
	{
		var generator = CreateGenerator();
		var book = generator.BuildBook(100, Settings);

		var population = generator.BuildPopulation(book, Settings);

		Assert.Equal(book.Asks.Concat(book.Bids).Select(l => l.Price), population.Select(l => l.Price));
		Assert.All(population, level => Assert.True(level.BotCount >= 1));
		Assert.All(
			population,
			level =>
				Assert.InRange(level.Balance, level.BotCount * Settings.MeanBotBalance * 0.5, level.BotCount * Settings.MeanBotBalance * 1.5)
		);
	}

	[Fact]
	public void NextPrice_StaysPositiveAndOnTheTickGrid()
	{
		var generator = CreateGenerator();
		var price = Settings.StartPrice;

		for (var step = 0; step < 2_000; step++)
		{
			price = generator.NextPrice(price, Settings);

			Assert.True(price >= Settings.TickSize);
			Assert.Equal(SyntheticMarketGenerator.RoundToTick(price, Settings.TickSize), price);
		}
	}
}
