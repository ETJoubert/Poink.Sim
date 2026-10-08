using System;
using System.Collections.Generic;
using System.Linq;
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

	private static BarPriceHistogram DwellHistogram(double price, double seconds, int direction = 1)
	{
		var histogram = new BarPriceHistogram(RowHeight);
		histogram.Add(price, direction, 0);
		histogram.AddDwell(price, seconds);
		return histogram;
	}

	// Older column: 10s at 10.5 (row 10-11); latest column: 30s at 12.5 (row 12-13); one tick each, so Time×Tk = seconds.
	private static XGridResult TimeTickGrid(double latestClose, int olderDirection = 1)
	{
		var grid = XGridStats.Compute(
			new List<HlocAvdtBar> { Bar(0, 10.5), Bar(1, latestClose) },
			new List<BarPriceHistogram> { DwellHistogram(10.5, 10, olderDirection), DwellHistogram(12.5, 30) },
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
	public void TimeTickReading_LatestColumnOnly_IsOneCellAcrossItsRow()
	{
		var reading = ColumnZoneHelper.TimeTickReading(TimeTickGrid(latestClose: 12.5), columnsBack: 0, 25);

		Assert.NotNull(reading);
		Assert.Equal((12.0, 13.0), (reading.Low, reading.High));
		Assert.Equal(0.5, reading.Position, 9);
		Assert.Equal(ColumnZone.Middle, reading.Zone);
		Assert.Equal(new[] { new ColumnZoneCell(12, 13, 30, 1) }, reading.Cells!);
	}

	[Fact]
	public void TimeTickReading_SpansEveryTradedRowOfTheColumnsWithOneCellPerRowLowToHigh()
	{
		var reading = ColumnZoneHelper.TimeTickReading(TimeTickGrid(latestClose: 12.5), columnsBack: 1, 25);

		Assert.NotNull(reading);
		Assert.Equal((10.0, 13.0), (reading.Low, reading.High));
		Assert.Equal(2.5 / 3, reading.Position, 9);
		Assert.Equal(ColumnZone.High, reading.Zone);
		Assert.Equal(
			new[] { new ColumnZoneCell(10, 11, 10, 10.0 / 30), new ColumnZoneCell(11, 12, 0, 0), new ColumnZoneCell(12, 13, 30, 1) },
			reading.Cells!
		);
	}

	[Fact]
	public void TimeTickReading_Split_SignsEachCellByItsUpMinusDownTimeTicks()
	{
		var reading = ColumnZoneHelper.TimeTickReading(TimeTickGrid(latestClose: 12.5, olderDirection: -1), columnsBack: 1, 25, split: true);

		Assert.NotNull(reading);
		Assert.Equal(
			new[] { new ColumnZoneCell(10, 11, -10, -10.0 / 30), new ColumnZoneCell(11, 12, 0, 0), new ColumnZoneCell(12, 13, 30, 1) },
			reading.Cells!
		);
	}

	[Fact]
	public void TotalCell_SumsTheRangesTimeTicksAtFullIntensity()
	{
		var reading = ColumnZoneHelper.TimeTickReading(TimeTickGrid(latestClose: 12.5), columnsBack: 1, 25);

		Assert.Equal(new ColumnZoneCell(10, 13, 40, 1), ColumnZoneHelper.TotalCell(reading));
	}

	[Fact]
	public void TotalCell_Split_NetsUpAgainstDownWithItsShareOfTheTotalAsIntensity()
	{
		var reading = ColumnZoneHelper.TimeTickReading(TimeTickGrid(latestClose: 12.5, olderDirection: -1), columnsBack: 1, 25, split: true);

		Assert.Equal(new ColumnZoneCell(10, 13, 20, 0.5), ColumnZoneHelper.TotalCell(reading));
	}

	[Fact]
	public void TotalCell_WithoutCells_IsNull()
	{
		Assert.Null(ColumnZoneHelper.TotalCell(null));
		Assert.Null(ColumnZoneHelper.TotalCell(new ColumnZoneReading(10, 14, 12, 0.5, ColumnZone.Middle)));
	}

	[Theory]
	[InlineData(9.5, ColumnZone.Below)]
	[InlineData(13.5, ColumnZone.Above)]
	public void TimeTickReading_PriceOutsideTheTimeTickRows_IsBelowOrAbove(double latestClose, ColumnZone expected)
	{
		var reading = ColumnZoneHelper.TimeTickReading(TimeTickGrid(latestClose), columnsBack: 1, 25);

		Assert.Equal(expected, reading?.Zone);
	}

	[Fact]
	public void TimeTickReading_WithoutAGrid_IsNull()
	{
		Assert.Null(ColumnZoneHelper.TimeTickReading(null, columnsBack: 1, 25));
	}

	private static ColumnZoneSettings GateOn(int threshold) => ColumnZoneHelper.DefaultSettings with { GateEnabled = true, GateThreshold = threshold };

	private static ColumnZoneRange?[] Columns(params double[] prices) =>
		prices.Select(p => (ColumnZoneRange?)new ColumnZoneRange(10, 14, p)).ToArray();

	[Theory]
	[InlineData(ColumnZone.Below, true, false)]
	[InlineData(ColumnZone.Low, true, false)]
	[InlineData(ColumnZone.Middle, false, false)]
	[InlineData(ColumnZone.High, false, true)]
	[InlineData(ColumnZone.Above, false, true)]
	[InlineData(null, false, false)]
	public void Supports_LongFromTheBottomAndShortFromTheTop(ColumnZone? zone, bool supportsLong, bool supportsShort)
	{
		Assert.Equal(supportsLong, ColumnZoneHelper.SupportsLong(zone));
		Assert.Equal(supportsShort, ColumnZoneHelper.SupportsShort(zone));
	}

	[Fact]
	public void Gate_AtThresholdOne_OpensASideWhenAnyColumnSupportsIt()
	{
		var gate = ColumnZoneHelper.Gate(Columns(10.5, 12, 13.5), GateOn(threshold: 1));

		Assert.Equal(1, gate.LongSupporters);
		Assert.Equal(1, gate.ShortSupporters);
		Assert.False(gate.LongLocked);
		Assert.False(gate.ShortLocked);
	}

	[Theory]
	[InlineData(2, false)]
	[InlineData(3, true)]
	public void Gate_OpensASideOnlyOnceItsSupportersReachH(int threshold, bool longLocked)
	{
		var gate = ColumnZoneHelper.Gate(Columns(10.5, 12, 9), GateOn(threshold));

		Assert.Equal(2, gate.LongSupporters);
		Assert.Equal(longLocked, gate.LongLocked);
		Assert.True(gate.ShortLocked);
	}

	[Fact]
	public void Gate_MiddleNeverCounts()
	{
		var gate = ColumnZoneHelper.Gate(Columns(12, 12), GateOn(threshold: 1));

		Assert.True(gate.LongLocked);
		Assert.True(gate.ShortLocked);
	}

	[Fact]
	public void Gate_UnreadableColumn_DoesNotCount()
	{
		var gate = ColumnZoneHelper.Gate(new ColumnZoneRange?[] { new(10, 14, 13.5), null }, GateOn(threshold: 2));

		Assert.Equal(1, gate.ShortSupporters);
		Assert.True(gate.ShortLocked);
	}

	[Fact]
	public void Gate_Disabled_NeverLocks()
	{
		var gate = ColumnZoneHelper.Gate(Columns(12, 12), ColumnZoneHelper.DefaultSettings);

		Assert.False(gate.LongLocked);
		Assert.False(gate.ShortLocked);
	}

	[Theory]
	[InlineData(0, 3, 1)]
	[InlineData(5, 3, 3)]
	[InlineData(2, 0, 1)]
	public void ClampGateThreshold_RunsFromOneToEveryColumn(int threshold, int columnCount, int expected)
	{
		Assert.Equal(expected, ColumnZoneHelper.ClampGateThreshold(threshold, columnCount));
	}
}
