using System;
using Poink.Sim.Simulation;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.Simulation;

public class QuartileBarAggregatorTests
{
	private static QuartileBarAggregator Fed(params double[] mids)
	{
		var aggregator = new QuartileBarAggregator(TimeSpan.FromMinutes(1));
		for (var i = 0; i < mids.Length; i++)
			aggregator.Feed(TestTickHelper.AtMid(i, mids[i]));
		return aggregator;
	}

	[Fact]
	public void Feed_RollingIntoNextBar_ReportsFiveNumberSummary()
	{
		var aggregator = Fed(3, 1, 5, 2, 4);

		var bar = aggregator.Feed(TestTickHelper.AtMid(60, 100))!.Value;

		Assert.Equal(1, bar.Min);
		Assert.Equal(2, bar.Q1);
		Assert.Equal(3, bar.Median);
		Assert.Equal(4, bar.Q3);
		Assert.Equal(5, bar.Max);
		Assert.Equal(5, bar.Count);
		Assert.Equal(TestTickHelper.Origin.AddSeconds(4), bar.CloseTime);
	}

	[Fact]
	public void Quartiles_InterpolateBetweenRanks()
	{
		var bar = Fed(1, 2, 3, 4).CurrentPartialBar!.Value;

		Assert.Equal(1.75, bar.Q1, 9);
		Assert.Equal(2.5, bar.Median, 9);
		Assert.Equal(3.25, bar.Q3, 9);
	}

	[Fact]
	public void SingleTick_CollapsesEveryQuartileToThatPrice()
	{
		var bar = Fed(7).CurrentPartialBar!.Value;

		Assert.Equal(7, bar.Min);
		Assert.Equal(7, bar.Q1);
		Assert.Equal(7, bar.Median);
		Assert.Equal(7, bar.Max);
	}

	[Fact]
	public void Reset_ClearsPartialBar()
	{
		var aggregator = Fed(1, 2);

		aggregator.Reset();

		Assert.Null(aggregator.CurrentPartialBar);
	}
}
