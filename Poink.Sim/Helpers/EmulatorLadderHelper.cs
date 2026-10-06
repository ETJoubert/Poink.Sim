using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Emulator;

namespace Poink.Sim.Helpers;

/// <summary>
/// One row of the price ladder, covering <see cref="Price"/> to <see cref="TopPrice"/> (the same price when a row is one
/// tick): the bid and ask volume resting there and the bots whose positions were entered there, if any.
/// </summary>
internal readonly record struct EmulatorLadderRow(
	double Price,
	double TopPrice,
	double? BidVolume,
	double? AskVolume,
	PositionLevel? Positions,
	bool IsCurrentPrice
);

/// <summary>
/// Lays an order book out on the shared market scale: one row per scale row, highest price first, each adding up the
/// book levels and positioned bots that fall inside it.
/// </summary>
internal static class EmulatorLadderHelper
{
	public static IReadOnlyList<EmulatorLadderRow> BuildRows(
		OrderBook book,
		IReadOnlyList<PositionLevel> positions,
		double price,
		EmulatorMarketScale scale
	)
	{
		var bids = VolumeByRow(book.Bids, scale);
		var asks = VolumeByRow(book.Asks, scale);
		var bots = PositionsByRow(positions, scale);
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

	private static Dictionary<int, PositionLevel> PositionsByRow(IReadOnlyList<PositionLevel> positions, EmulatorMarketScale scale) =>
		InScale(positions, level => level.Price, scale)
			.GroupBy(entry => entry.Row)
			.ToDictionary(
				group => group.Key,
				group => new PositionLevel(
					EmulatorTickHelper.FromTicks(scale.RowBottomTicks(group.Key), scale.TickSize),
					group.Sum(entry => entry.Item.LongBots),
					group.Sum(entry => entry.Item.ShortBots),
					group.Sum(entry => entry.Item.Equity)
				)
			);
}
