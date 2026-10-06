using System;
using System.Linq;
using Poink.Sim.Emulator;
using Poink.Sim.Helpers;
using Xunit;

namespace Poink.Sim.Tests.Helpers;

public class EmulatorMarketScaleHelperTests
{
	private static EmulatorPricePoint[] History(params double[] prices) =>
		prices.Select((price, i) => new EmulatorPricePoint(DateTime.UnixEpoch.AddSeconds(i), price)).ToArray();

	[Fact]
	public void ScaleOf_FitsHistoryAndBookWithoutCentringOnThePrice()
	{
		var book = new OrderBook([new OrderBookLevel(95, 1)], [new OrderBookLevel(140, 1)]);

		var scale = EmulatorMarketScaleHelper.ScaleOf(History(100, 120), 130, book, 1);

		Assert.Equal((94L, 141L, 2L), (scale.LowTicks, scale.HighTicks, scale.TicksPerRow));
		Assert.Equal(24, scale.RowCount);
		Assert.NotEqual(0.5, EmulatorChartHelper.YFraction(130, scale.PriceRange), 3);
	}

	[Fact]
	public void ScaleOf_NarrowRangeIsWidenedToTheMinimumRows()
	{
		var scale = EmulatorMarketScaleHelper.ScaleOf(History(100), 100, OrderBook.Empty, 1);

		Assert.Equal(EmulatorMarketScaleHelper.MinRows, scale.RowCount);
		Assert.Equal(1, scale.TicksPerRow);
		Assert.True(scale.Contains(100));
	}

	[Fact]
	public void ScaleOf_WideRangeGroupsTicksIntoAtMostMaxRows()
	{
		var scale = EmulatorMarketScaleHelper.ScaleOf(History(100, 1_000), 500, OrderBook.Empty, 1);

		Assert.InRange(scale.RowCount, 1, EmulatorMarketScaleHelper.MaxRows);
		Assert.True(scale.Contains(100) && scale.Contains(1_000));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(5)]
	[InlineData(19)]
	public void PriceRange_PutsEachRowsCentreWhereItsPriceFallsOnTheChart(int row)
	{
		var scale = new EmulatorMarketScale(80, 99, 1, 1);
		var rowCentreFraction = (row + 0.5) / scale.RowCount;

		Assert.Equal(rowCentreFraction, EmulatorChartHelper.YFraction(scale.RowTopTicks(row), scale.PriceRange), 9);
	}
}
