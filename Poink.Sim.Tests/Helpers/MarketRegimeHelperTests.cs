using System.Linq;
using Poink.Sim.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.Helpers;

public class MarketRegimeHelperTests
{
	private static HlocAvdtBar[] Closes(params double[] closes) => closes.Select(TestBarHelper.Closing).ToArray();

	[Fact]
	public void AtrSeries_AveragesTrueRangeIncludingGaps()
	{
		HlocAvdtBar[] bars =
		[
			TestBarHelper.Bar(11, 9, 10),
			TestBarHelper.Bar(12, 10, 11),
			TestBarHelper.Bar(13, 11, 12),
			TestBarHelper.Bar(20, 18, 19),
		];

		var atr = MarketRegimeHelper.AtrSeries(bars, 2);

		Assert.Equal(new[] { 2.0, 2.0, 5.0 }, atr);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(5)]
	public void AtrSeries_InvalidOrUnfilledPeriod_IsEmpty(int period)
	{
		Assert.Empty(MarketRegimeHelper.AtrSeries(Closes(1, 2, 3), period));
	}

	[Fact]
	public void Percentile_RanksLatestValueAgainstSeries()
	{
		Assert.Equal(100, MarketRegimeHelper.Percentile([1, 2, 3, 4]));
		Assert.Equal(25, MarketRegimeHelper.Percentile([4, 3, 2, 1]));
		Assert.Null(MarketRegimeHelper.Percentile([1]));
	}

	[Fact]
	public void EfficiencyRatio_StraightLineIsOne_ChopIsLower()
	{
		Assert.Equal(1, MarketRegimeHelper.EfficiencyRatio(Closes(10, 11, 12, 13), 3)!.Value, 9);
		Assert.Equal(1.0 / 3, MarketRegimeHelper.EfficiencyRatio(Closes(10, 12, 10, 12), 3)!.Value, 9);
	}

	[Fact]
	public void EfficiencyRatio_FlatOrTooShort_IsNull()
	{
		Assert.Null(MarketRegimeHelper.EfficiencyRatio(Closes(5, 5, 5), 2));
		Assert.Null(MarketRegimeHelper.EfficiencyRatio(Closes(1, 2), 2));
	}

	[Fact]
	public void Regression_PerfectTrend_HasUnitFit()
	{
		var (slope, rSquared) = MarketRegimeHelper.Regression(Closes(1, 3, 5, 7))!.Value;

		Assert.Equal(2, slope, 9);
		Assert.Equal(1, rSquared, 9);
	}

	[Fact]
	public void Regression_FlatOrTooShort_IsZeroOrNull()
	{
		Assert.Equal((0.0, 0.0), MarketRegimeHelper.Regression(Closes(5, 5, 5)));
		Assert.Null(MarketRegimeHelper.Regression(Closes(1, 2)));
	}

	[Fact]
	public void VolumeImbalance_IsNetDirectionalShare()
	{
		HlocAvdtBar[] bars = [TestBarHelper.Bar(1, 1, 1, upVolume: 2, downVolume: 1), TestBarHelper.Bar(1, 1, 1, upVolume: 1)];

		Assert.Equal(0.5, MarketRegimeHelper.VolumeImbalance(bars)!.Value, 9);
		Assert.Null(MarketRegimeHelper.VolumeImbalance(Closes(1, 2)));
	}
}
