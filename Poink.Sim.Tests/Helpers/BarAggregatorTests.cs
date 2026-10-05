using System;
using System.Linq;
using Poink.Sim.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.Helpers;

public class BarAggregatorTests
{
	private static readonly Tick[] Ticks = new[] { 10.0, 12, 9, 11, 13 }.Select((mid, i) => TestTickHelper.AtMid(i, mid)).ToArray();

	[Fact]
	public void Ohlc_ReportsOpenHighLowCloseAndTickFlow()
	{
		var aggregated = BarAggregator.Aggregate(Ticks, 0, BarAggregationKind.Ohlc)!.Value;

		var bar = aggregated.Bar;
		Assert.Equal(TestTickHelper.Origin, bar.TimeUtc);
		Assert.Equal(10, bar.Open);
		Assert.Equal(13, bar.High);
		Assert.Equal(9, bar.Low);
		Assert.Equal(13, bar.Close);
		Assert.Equal(5, bar.Volume);
		Assert.Equal(3, bar.UpVolume);
		Assert.Equal(1, bar.DownVolume);
	}

	[Fact]
	public void Quartile_CarriesInterquartileBoundsInOpenAndClose()
	{
		var aggregated = BarAggregator.Aggregate(Ticks, 0, BarAggregationKind.Quartile)!.Value;

		Assert.Equal(10, aggregated.Bar.Open);
		Assert.Equal(12, aggregated.Bar.Close);
		Assert.Equal(13, aggregated.Bar.High);
		Assert.Equal(9, aggregated.Bar.Low);
	}

	[Fact]
	public void Decile_InterpolatesTenthAndNinetiethPercentiles()
	{
		var aggregated = BarAggregator.Aggregate(Ticks, 0, BarAggregationKind.Decile)!.Value;

		Assert.Equal(9.4, aggregated.Bar.Open, 9);
		Assert.Equal(12.6, aggregated.Bar.Close, 9);
	}

	[Fact]
	public void MeanStdDev_ReplacesHighLowWithBandButKeepsTrueExtremes()
	{
		var aggregated = BarAggregator.Aggregate(Ticks, 0, BarAggregationKind.MeanStdDev)!.Value;

		Assert.Equal(11, aggregated.Bar.Open, 9);
		Assert.Equal(11 + Math.Sqrt(2), aggregated.Bar.High, 9);
		Assert.Equal(11 - Math.Sqrt(2), aggregated.Bar.Low, 9);
		Assert.Equal(13, aggregated.TrueHigh);
		Assert.Equal(9, aggregated.TrueLow);
	}

	[Fact]
	public void Aggregate_FromStartIndex_IgnoresEarlierTicks()
	{
		var aggregated = BarAggregator.Aggregate(Ticks, 3, BarAggregationKind.Ohlc)!.Value;

		Assert.Equal(11, aggregated.Bar.Open);
		Assert.Equal(2, aggregated.Bar.Volume);
		Assert.Equal(TestTickHelper.Origin.AddSeconds(3), aggregated.Bar.TimeUtc);
	}

	[Fact]
	public void Aggregate_StartPastEnd_ReturnsNull()
	{
		Assert.Null(BarAggregator.Aggregate(Ticks, 5, BarAggregationKind.Ohlc));
	}
}
