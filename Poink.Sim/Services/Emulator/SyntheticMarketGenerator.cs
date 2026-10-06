using System;
using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Emulator;

/// <summary>
/// Gaussian random walk on the tick grid, with a book of whole-unit volumes that deepens away from the touch
/// and a bot population per level that grows with the volume resting there.
/// </summary>
public sealed class SyntheticMarketGenerator : ISyntheticMarketGenerator
{
	private const double BaseLevelVolume = 20;
	private const double DepthGrowthPerLevel = 0.15;

	private readonly Random _random;

	public SyntheticMarketGenerator()
		: this(new Random()) { }

	internal SyntheticMarketGenerator(Random random)
	{
		_random = random;
	}

	public double NextPrice(double price, EmulatorSettings settings)
	{
		var moveTicks = Math.Round(NextGaussian() * settings.VolatilityTicks);
		var next = RoundToTick(price + moveTicks * settings.TickSize, settings.TickSize);
		return Math.Max(settings.TickSize, next);
	}

	public OrderBook BuildBook(double price, EmulatorSettings settings)
	{
		var tick = settings.TickSize;
		var halfSpreadTicks = Math.Max(1, settings.SpreadTicks) / 2.0;
		var bestBid = RoundToTick(price - Math.Ceiling(halfSpreadTicks) * tick, tick);
		var bestAsk = RoundToTick(bestBid + Math.Max(1, settings.SpreadTicks) * tick, tick);

		var bids = new List<OrderBookLevel>(settings.BookDepth);
		var asks = new List<OrderBookLevel>(settings.BookDepth);
		for (var level = 0; level < settings.BookDepth; level++)
		{
			var bidPrice = RoundToTick(bestBid - level * tick, tick);
			if (bidPrice > 0)
			{
				bids.Add(new OrderBookLevel(bidPrice, LevelVolume(level)));
			}
			asks.Add(new OrderBookLevel(RoundToTick(bestAsk + level * tick, tick), LevelVolume(level)));
		}

		return new OrderBook(bids, asks);
	}

	public IReadOnlyList<PopulationLevel> BuildPopulation(OrderBook book, EmulatorSettings settings)
	{
		var population = new List<PopulationLevel>(book.Bids.Count + book.Asks.Count);
		foreach (var level in book.Asks.Concat(book.Bids))
		{
			var botCount = (int)Math.Max(1, Math.Round(level.Volume / Math.Max(1, settings.VolumePerBot) * (0.5 + _random.NextDouble())));
			var balance = Enumerable.Range(0, botCount).Sum(_ => settings.MeanBotBalance * (0.5 + _random.NextDouble()));
			population.Add(new PopulationLevel(level.Price, botCount, Math.Round(balance, 2)));
		}

		return population;
	}

	internal static double RoundToTick(double price, double tickSize) => Math.Round(Math.Round(price / tickSize) * tickSize, 10);

	private double LevelVolume(int level)
	{
		var scale = 0.5 + _random.NextDouble();
		return Math.Max(1, Math.Round(BaseLevelVolume * (1 + level * DepthGrowthPerLevel) * scale));
	}

	private double NextGaussian()
	{
		var u1 = 1.0 - _random.NextDouble();
		var u2 = _random.NextDouble();
		return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
	}
}
