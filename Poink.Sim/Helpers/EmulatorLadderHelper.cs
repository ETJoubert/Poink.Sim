using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Emulator;

namespace Poink.Sim.Helpers;

/// <summary>
/// One row of the price ladder, covering <see cref="Price"/> to <see cref="TopPrice"/> (the same price when a row is one
/// tick): the bid and ask volume resting there and the bots with orders there, if any.
/// </summary>
internal readonly record struct EmulatorLadderRow(
	double Price,
	double TopPrice,
	double? BidVolume,
	double? AskVolume,
	PopulationLevel? Population,
	bool IsCurrentPrice
);

/// <summary>
/// Lays an order book out on the shared market scale: one row per scale row, highest price first, each adding up the
/// book levels and bots that fall inside it.
/// </summary>
internal static class EmulatorLadderHelper
{
	public static IReadOnlyList<EmulatorLadderRow> BuildRows(
		OrderBook book,
		IReadOnlyList<PopulationLevel> population,
		double price,
		EmulatorMarketScale scale
	)
	{
		var bids = VolumeByRow(book.Bids, scale);
		var asks = VolumeByRow(book.Asks, scale);
		var bots = PopulationByRow(population, scale);
		var priceTicks = EmulatorTickHelper.ToTicks(price, scale.TickSize);
		var priceRow = scale.Contains(priceTicks) ? scale.RowOf(priceTicks) : -1;

		return Enumerable
			.Range(0, scale.RowCount)
			.Select(row => new EmulatorLadderRow(
				EmulatorTickHelper.FromTicks(scale.RowBottomTicks(row), scale.TickSize),
				EmulatorTickHelper.FromTicks(scale.RowTopTicks(row), scale.TickSize),
				bids.TryGetValue(row, out var bid) ? bid : null,
				asks.TryGetValue(row, out var ask) ? ask : null,
				bots.TryGetValue(row, out var level) ? level : null,
				row == priceRow
			))
			.ToArray();
	}

	private static IEnumerable<(int Row, T Item)> InScale<T>(IEnumerable<T> items, Func<T, double> priceOf, EmulatorMarketScale scale) =>
		items
			.Select(item => (Ticks: EmulatorTickHelper.ToTicks(priceOf(item), scale.TickSize), Item: item))
			.Where(entry => scale.Contains(entry.Ticks))
			.Select(entry => (scale.RowOf(entry.Ticks), entry.Item));

	private static Dictionary<int, double> VolumeByRow(IReadOnlyList<OrderBookLevel> levels, EmulatorMarketScale scale) =>
		InScale(levels, level => level.Price, scale)
			.GroupBy(entry => entry.Row)
			.ToDictionary(group => group.Key, group => group.Sum(entry => entry.Item.Volume));

	private static Dictionary<int, PopulationLevel> PopulationByRow(IReadOnlyList<PopulationLevel> population, EmulatorMarketScale scale) =>
		InScale(population, level => level.Price, scale)
			.GroupBy(entry => entry.Row)
			.ToDictionary(
				group => group.Key,
				group => new PopulationLevel(
					EmulatorTickHelper.FromTicks(scale.RowBottomTicks(group.Key), scale.TickSize),
					group.Sum(entry => entry.Item.BotCount),
					group.Sum(entry => entry.Item.Balance)
				)
			);
}
