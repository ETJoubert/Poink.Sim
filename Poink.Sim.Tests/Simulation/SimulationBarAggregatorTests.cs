using System;
using Poink.Sim.Simulation;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.Simulation;

public class SimulationBarAggregatorTests
{
	private static SimulationBarAggregator FedOneMinute()
	{
		var aggregator = new SimulationBarAggregator(TimeSpan.FromMinutes(1), 1.0);
		aggregator.Feed(TestTickHelper.AtMid(0, 10));
		aggregator.Feed(TestTickHelper.AtMid(20, 12));
		aggregator.Feed(TestTickHelper.AtMid(40, 9));
		aggregator.Feed(TestTickHelper.AtMid(50, 11));
		return aggregator;
	}

	[Fact]
	public void Feed_WithinBar_CompletesNothing()
	{
		var aggregator = new SimulationBarAggregator(TimeSpan.FromMinutes(1), 1.0);

		Assert.Null(aggregator.Feed(TestTickHelper.AtMid(0, 10)));
		Assert.Null(aggregator.Feed(TestTickHelper.AtMid(30, 11)));
	}

	[Fact]
	public void Feed_RollingIntoNextBar_CompletesBarWithOhlcAndFlow()
	{
		var aggregator = FedOneMinute();

		var completed = aggregator.Feed(TestTickHelper.AtMid(65, 15));

		var bar = completed!.Value.Bar;
		Assert.Equal(TestTickHelper.Origin, bar.OpenTime);
		Assert.Equal(TestTickHelper.Origin.AddSeconds(50), bar.CloseTime);
		Assert.Equal(TestTickHelper.Origin.AddSeconds(20), bar.HighTime);
		Assert.Equal(TestTickHelper.Origin.AddSeconds(40), bar.LowTime);
		Assert.Equal(10, bar.Open);
		Assert.Equal(12, bar.High);
		Assert.Equal(9, bar.Low);
		Assert.Equal(11, bar.Close);
		Assert.Equal(10.5, bar.Average);
		Assert.Equal(4, bar.Volume);
		Assert.Equal(7, bar.Distance);
		Assert.Equal(2, bar.UpVolume);
		Assert.Equal(1, bar.DownVolume);
	}

	[Fact]
	public void Feed_RollingIntoNextBar_CompletesHistogramBookedToOriginBuckets()
	{
		var aggregator = FedOneMinute();

		var histogram = aggregator.Feed(TestTickHelper.AtMid(65, 15))!.Value.Histogram;

		Assert.Equal(3, histogram.Buckets.Count);
		Assert.Equal(2, histogram.Buckets[10].Count);
		Assert.Equal(1, histogram.Buckets[10].UpCount);
		Assert.Equal(2, histogram.Buckets[10].UpDistance);
		Assert.Equal(1, histogram.Buckets[12].DownCount);
		Assert.Equal(3, histogram.Buckets[12].DownDistance);
		Assert.Equal(1, histogram.Buckets[9].UpCount);
	}

	[Fact]
	public void CurrentPartialBar_AfterRollover_StartsFromNewTick()
	{
		var aggregator = FedOneMinute();
		aggregator.Feed(TestTickHelper.AtMid(65, 15));

		var partial = aggregator.CurrentPartialBar!.Value;

		Assert.Equal(TestTickHelper.Origin.AddMinutes(1), partial.OpenTime);
		Assert.Equal(15, partial.Open);
		Assert.Equal(1, partial.Volume);
	}

	[Fact]
	public void Reset_ClearsPartialBar()
	{
		var aggregator = FedOneMinute();

		aggregator.Reset();

		Assert.Null(aggregator.CurrentPartialBar);
		Assert.Null(aggregator.CurrentPartialHistogram);
		Assert.Null(aggregator.Feed(TestTickHelper.AtMid(65, 15)));
	}
}
