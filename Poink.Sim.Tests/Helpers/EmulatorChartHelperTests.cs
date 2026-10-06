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
