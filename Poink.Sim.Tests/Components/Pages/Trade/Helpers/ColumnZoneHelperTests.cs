using System;
using System.Collections.Generic;
using Poink.Sim.Components.Charts;
using Poink.Sim.Components.Pages.Trade.Helpers;
using Poink.Sim.Components.Pages.Trade.Models;
using Poink.Sim.Models;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.Components.Pages.Trade.Helpers;

public class ColumnZoneHelperTests
{
	private const double RowHeight = 1.0;

	private static HlocAvdtBar Bar(int minute, double close)
	{
		var open = TestTickHelper.Origin.AddMinutes(minute);
		return new HlocAvdtBar(open, open.AddSeconds(59), open, open, close, close, close, close, close, 1, 0, 0, 0);
	}

	private static BarPriceHistogram Histogram(params double[] prices)
	{
		var histogram = new BarPriceHistogram(RowHeight);
		foreach (var price in prices)
			histogram.Add(price, 1, 0);
		return histogram;
	}

	private static XGridResult TwoColumnGrid(double latestClose, params double[] latestPrices)
	{
		var bars = new List<HlocAvdtBar> { Bar(0, 10.5), Bar(1, latestClose) };
		var histograms = new List<BarPriceHistogram> { Histogram(10.5, 14.5), Histogram(latestPrices) };

		var grid = XGridStats.Compute(
			bars,
			histograms,
			new List<WeightedPriceSample>(),
			DateTime.MinValue,
			DateTime.MaxValue,
			RowHeight,
			colCount: 2
		);

		Assert.NotNull(grid);
		return grid.Value;
	}

	[Fact]
	public void RecentColumns_SpansEachColumnsOwnTradedRowsLatestFirst()
	{
		var recent = ColumnZoneHelper.RecentColumns(TwoColumnGrid(12.25, 11.5, 12.25, 12.5), columnsBack: 1);

		Assert.Equal(new ColumnZoneRange?[] { new(11, 13, 12.25), new(10, 15, 12.25) }, recent);
	}

	[Fact]
	public void RecentColumns_PastTheGridsColumns_PadsWithNull()
	{
		var recent = ColumnZoneHelper.RecentColumns(TwoColumnGrid(12.25, 12.25), columnsBack: 3);

		Assert.Equal(4, recent.Count);
		Assert.NotNull(recent[1]);
		Assert.Null(recent[2]);
		Assert.Null(recent[3]);
	}

	[Fact]
	public void RecentColumns_WithoutAGrid_HoldsOnlyNulls()
	{
		Assert.All(ColumnZoneHelper.RecentColumns(null, columnsBack: 2), Assert.Null);
	}

	[Theory]
	[InlineData(-1, 0)]
	[InlineData(3, 3)]
	[InlineData(ColumnZoneHelper.MaxColumnsBack + 5, ColumnZoneHelper.MaxColumnsBack)]
	public void ClampColumnsBack_KeepsKWithinItsBounds(int columnsBack, int expected)
	{
		Assert.Equal(expected, ColumnZoneHelper.ClampColumnsBack(columnsBack));
	}

	[Theory]
	[InlineData(0, "n")]
	[InlineData(2, "n-2")]
	public void ColumnLabel_NamesColumnsFromTheLatest(int columnsBack, string expected)
	{
		Assert.Equal(expected, ColumnZoneHelper.ColumnLabel(columnsBack));
	}

	[Theory]
	[InlineData(2)]
	[InlineData(-1)]
	public void ColumnRange_PastTheGridsColumns_IsNull(int columnsBack)
	{
		Assert.Null(ColumnZoneHelper.ColumnRange(TwoColumnGrid(12.25, 12.25), columnsBack));
	}

	[Theory]
	[InlineData(9.5, ColumnZone.Below)]
	[InlineData(10, ColumnZone.Low)]
	[InlineData(12, ColumnZone.Middle)]
	[InlineData(14, ColumnZone.High)]
	[InlineData(14.5, ColumnZone.Above)]
	public void ZoneOf_Range_FlagsAPriceOutsideTheSpan(double price, ColumnZone expected)
	{
		Assert.Equal(expected, ColumnZoneHelper.ZoneOf(new ColumnZoneRange(10, 14, price), 25));
	}

	[Theory]
	[InlineData(10, 0)]
	[InlineData(12, 0.5)]
	[InlineData(14, 1)]
	[InlineData(9, 0)]
	[InlineData(15, 1)]
	public void PricePosition_MapsTheSpanOntoZeroToOne(double price, double expected)
	{
		Assert.Equal(expected, ColumnZoneHelper.PricePosition(new ColumnZoneRange(10, 14, price)), 9);
	}

	[Fact]
	public void PricePosition_FlatSpan_ReadsAsTheMiddle()
	{
		Assert.Equal(0.5, ColumnZoneHelper.PricePosition(new ColumnZoneRange(10, 10, 10)));
	}

	[Theory]
	[InlineData(0.0, 25, ColumnZone.Low)]
	[InlineData(0.25, 25, ColumnZone.Low)]
	[InlineData(0.26, 25, ColumnZone.Middle)]
	[InlineData(0.74, 25, ColumnZone.Middle)]
	[InlineData(0.75, 25, ColumnZone.High)]
	[InlineData(1.0, 25, ColumnZone.High)]
	[InlineData(0.15, 10, ColumnZone.Middle)]
	[InlineData(0.5, 50, ColumnZone.Low)]
	[InlineData(0.51, 50, ColumnZone.High)]
	public void ZoneOf_SplitsTheSpanIntoBottomMiddleAndTopShares(double position, double zonePercent, ColumnZone expected)
	{
		Assert.Equal(expected, ColumnZoneHelper.ZoneOf(position, zonePercent));
	}

	[Theory]
	[InlineData(0, 1)]
	[InlineData(80, 50)]
	[InlineData(33.4, 33)]
	[InlineData(double.NaN, 25)]
	public void ClampZonePercent_KeepsTheShareWithinItsBounds(double percent, double expected)
	{
		Assert.Equal(expected, ColumnZoneHelper.ClampZonePercent(percent));
	}
}
