using System;
using Poink.Sim.Emulator;
using Poink.Sim.Helpers;
using Xunit;

namespace Poink.Sim.Tests.Helpers;

public class EmulatorChartHelperTests
{
	private static readonly EmulatorPricePoint[] History =
	[
		new(DateTime.UnixEpoch, 100),
		new(DateTime.UnixEpoch.AddSeconds(1), 110),
		new(DateTime.UnixEpoch.AddSeconds(2), 105),
	];

	[Theory]
	[InlineData(120, 0)]
	[InlineData(100, 1)]
	[InlineData(110, 0.5)]
	[InlineData(200, 0)]
	public void YFraction_MapsTopOfRangeToZero(double price, double expected)
	{
		Assert.Equal(expected, EmulatorChartHelper.YFraction(price, new EmulatorPriceRange(100, 120)), 9);
	}

	[Fact]
	public void PolylinePoints_SpreadsHistoryAcrossWidth()
	{
		var points = EmulatorChartHelper.PolylinePoints(History, new EmulatorPriceRange(100, 110), 100, 50);

		Assert.Equal("0,50 50,0 100,25", points);
	}

	[Theory]
	[InlineData(0.01, "F2")]
	[InlineData(0.00001, "F5")]
	[InlineData(1, "F0")]
	[InlineData(5, "F0")]
	public void PriceFormat_ShowsTheTickSizesDecimals(double tickSize, string expected)
	{
		Assert.Equal(expected, EmulatorChartHelper.PriceFormat(tickSize));
	}

	[Fact]
	public void GroupBars_FoldsPointsIntoClockAlignedBars()
	{
		var start = DateTime.UnixEpoch;
		EmulatorPricePoint[] history =
		[
			new(start, 10, 12, 9, 11),
			new(start.AddMilliseconds(400), 11, 15, 11, 14),
			new(start.AddMilliseconds(500), 14, 14, 8, 9),
		];

		var bars = EmulatorChartHelper.GroupBars(history, TimeSpan.FromMilliseconds(500));

		Assert.Equal([new EmulatorPricePoint(start, 10, 15, 9, 14), history[2]], bars);
	}

	[Fact]
	public void OhlcPaths_SplitsRisingAndFallingBars()
	{
		EmulatorPricePoint[] bars = [new(DateTime.UnixEpoch, 100, 110, 100, 110), new(DateTime.UnixEpoch, 110, 110, 100, 100)];

		var (rising, falling) = EmulatorChartHelper.OhlcPaths(bars, new EmulatorPriceRange(100, 110), 100, 10);

		Assert.Equal("M25,0V10M7.5,10H25M25,0H42.5", rising);
		Assert.Equal("M75,0V10M57.5,0H75M75,10H92.5", falling);
	}

	[Fact]
	public void DepthPercent_IsShareOfDeepestLevel()
	{
		Assert.Equal(25, EmulatorChartHelper.DepthPercent(10, 40));
		Assert.Equal(0, EmulatorChartHelper.DepthPercent(10, 0));
	}

	[Theory]
	[InlineData(950, "950")]
	[InlineData(54_321, "54.3k")]
	[InlineData(2_500_000, "2.5M")]
	public void CompactAmount_ShortensThousandsAndMillions(double amount, string expected)
	{
		Assert.Equal(expected, EmulatorChartHelper.CompactAmount(amount));
	}
}
