using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Emulator;

namespace Poink.Sim.Helpers;

/// <summary>One tick of the price ladder: the bid and ask volume resting at that price and the bots there, if any.</summary>
internal readonly record struct EmulatorLadderRow(
	double Price,
	double? BidVolume,
	double? AskVolume,
	PopulationLevel? Population,
	bool IsCurrentPrice
);

/// <summary>
/// Lays an order book out as a price ladder centred on the current price: one row per tick, asks above and bids below,
/// with the same number of rows on each side so the current price stays in the middle.
/// </summary>
internal static class EmulatorLadderHelper
{
	public static IReadOnlyList<EmulatorLadderRow> BuildRows(
		OrderBook book,
		IReadOnlyList<PopulationLevel> population,
		double price,
		double tickSize
	)
	{
		var centre = SyntheticMarketGenerator.RoundToTick(price, tickSize);
		var bids = VolumeByTickOffset(book.Bids, centre, tickSize);
		var asks = VolumeByTickOffset(book.Asks, centre, tickSize);
		var bots = PopulationByTickOffset(population, centre, tickSize);
		var rowsEachSide = bids.Keys.Concat(asks.Keys).Concat(bots.Keys).Select(Math.Abs).DefaultIfEmpty(0).Max();

		var rows = new List<EmulatorLadderRow>((int)(rowsEachSide * 2 + 1));
		for (var offset = rowsEachSide; offset >= -rowsEachSide; offset--)
		{
			var rowPrice = SyntheticMarketGenerator.RoundToTick(centre + offset * tickSize, tickSize);
			rows.Add(
				new EmulatorLadderRow(
					rowPrice,
					bids.TryGetValue(offset, out var bid) ? bid : null,
					asks.TryGetValue(offset, out var ask) ? ask : null,
					bots.TryGetValue(offset, out var level) ? level with { Price = rowPrice } : null,
					offset == 0
				)
			);
		}

		return rows;
	}

	private static long TickOffset(double price, double centre, double tickSize) => (long)Math.Round((price - centre) / tickSize);

	private static Dictionary<long, double> VolumeByTickOffset(
		IReadOnlyList<OrderBookLevel> levels,
		double centre,
		double tickSize
	) =>
		levels
			.GroupBy(level => TickOffset(level.Price, centre, tickSize))
			.ToDictionary(group => group.Key, group => group.Sum(level => level.Volume));

	private static Dictionary<long, PopulationLevel> PopulationByTickOffset(
		IReadOnlyList<PopulationLevel> population,
		double centre,
		double tickSize
	) =>
		population
			.GroupBy(level => TickOffset(level.Price, centre, tickSize))
			.ToDictionary(
				group => group.Key,
				group => new PopulationLevel(group.First().Price, group.Sum(l => l.BotCount), group.Sum(l => l.Balance))
			);
}
