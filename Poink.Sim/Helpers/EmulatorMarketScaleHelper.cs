using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Emulator;

namespace Poink.Sim.Helpers;

/// <summary>
/// The price scale the chart and the ladder share: ticks <see cref="LowTicks"/> to <see cref="HighTicks"/>, split into
/// equal ladder rows of <see cref="TicksPerRow"/> ticks each, highest first. Each row's centre sits exactly where its
/// price falls on the chart.
/// </summary>
public sealed record EmulatorMarketScale(long LowTicks, long HighTicks, long TicksPerRow, double TickSize)
{
	public int RowCount => (int)((HighTicks - LowTicks + 1) / TicksPerRow);

	public EmulatorPriceRange PriceRange => new((LowTicks - 0.5) * TickSize, (HighTicks + 0.5) * TickSize);

	public bool Contains(long ticks) => ticks >= LowTicks && ticks <= HighTicks;

	public int RowOf(long ticks) => (int)((HighTicks - ticks) / TicksPerRow);

	public long RowTopTicks(int row) => HighTicks - row * TicksPerRow;

	public long RowBottomTicks(int row) => RowTopTicks(row) - TicksPerRow + 1;
}

/// <summary>
/// Fits one price scale for the chart and the ladder to share: the price history (with the last price and the spread)
/// fills the middle half, and the quarters above and below are left for the order book. Book levels beyond those margins
/// fall off the scale rather than squeezing the history.
/// </summary>
internal static class EmulatorMarketScaleHelper
{
	public const int MinRows = 20;
	public const int MaxRows = 40;
	public const double BookMarginShare = 0.5;
	public const long MinBookMarginTicks = 5;

	public static EmulatorMarketScale ScaleOf(
		IReadOnlyList<EmulatorPricePoint> history,
		double price,
		OrderBook book,
		double tickSize
	)
	{
		var bandTicks = history
			.Select(point => point.Price)
			.Append(price)
			.Concat(new[] { book.BestBid, book.BestAsk }.OfType<double>())
			.Select(p => EmulatorTickHelper.ToTicks(p, tickSize))
			.ToArray();
		var bandLow = bandTicks.Min();
		var bandHigh = bandTicks.Max();
		var margin = Math.Max(MinBookMarginTicks, (long)Math.Ceiling((bandHigh - bandLow + 1) * BookMarginShare));
		var low = bandLow - margin;
		var high = bandHigh + margin;

		var shortfall = MinRows - (high - low + 1);
		if (shortfall > 0)
		{
			low -= shortfall / 2;
			high += shortfall - shortfall / 2;
		}

		var ticksPerRow = Math.Max(1, (long)Math.Ceiling((high - low + 1) / (double)MaxRows));
		var alignedLow = (long)Math.Floor(low / (double)ticksPerRow) * ticksPerRow;
		var rows = (long)Math.Ceiling((high - alignedLow + 1) / (double)ticksPerRow);
		return new EmulatorMarketScale(alignedLow, alignedLow + rows * ticksPerRow - 1, ticksPerRow, tickSize);
	}
}
