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
	public void ScaleOf_PutsTheHistoryInTheMiddleHalfWithBookMarginsAboveAndBelow()
	{
		var book = new OrderBook([new OrderBookLevel(99, 1)], [new OrderBookLevel(111, 1)]);

		var scale = EmulatorMarketScaleHelper.ScaleOf(History(100, 110), 105, book, 1);

		Assert.Equal((92L, 118L, 1L), (scale.LowTicks, scale.HighTicks, scale.TicksPerRow));
		Assert.InRange(EmulatorChartHelper.YFraction(111, scale.PriceRange), 0.2, 0.3);
		Assert.InRange(EmulatorChartHelper.YFraction(99, scale.PriceRange), 0.7, 0.8);
	}

	[Fact]
	public void ScaleOf_FarBookLevelsFallOffRatherThanSqueezeTheHistory()
	{
		var book = new OrderBook(
			[new OrderBookLevel(109, 1), new OrderBookLevel(10, 1)],
			[new OrderBookLevel(111, 1), new OrderBookLevel(500, 1)]
		);

		var scale = EmulatorMarketScaleHelper.ScaleOf(History(100, 120), 110, book, 1);

		Assert.False(scale.Contains(10) || scale.Contains(500));
		Assert.True(scale.Contains(100) && scale.Contains(120));
	}

	[Fact]
	public void ScaleOf_LeavesAtLeastTheMinimumBookMargin()
	{
		var scale = EmulatorMarketScaleHelper.ScaleOf(History(100), 100, OrderBook.Empty, 1);

		Assert.True(scale.Contains(100 - EmulatorMarketScaleHelper.MinBookMarginTicks));
		Assert.True(scale.Contains(100 + EmulatorMarketScaleHelper.MinBookMarginTicks));
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
