using Poink.Sim.Simulation;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.Simulation;

public class PriceCellTrackerTests
{
	[Fact]
	public void Feed_FirstTick_EstablishesCellWithoutReporting()
	{
		var tracker = new PriceCellTracker(1.0);

		Assert.Null(tracker.Feed(TestTickHelper.AtMid(0, 10.5)));
	}

	[Fact]
	public void Feed_WithinSameCell_ReportsNothing()
	{
		var tracker = new PriceCellTracker(1.0);
		tracker.Feed(TestTickHelper.AtMid(0, 10.25));

		Assert.Null(tracker.Feed(TestTickHelper.AtMid(1, 10.75)));
	}

	[Fact]
	public void Feed_CrossingIntoNewCell_ReportsItsBounds()
	{
		var tracker = new PriceCellTracker(0.25);
		tracker.Feed(TestTickHelper.AtMid(0, 10.1));

		var entered = tracker.Feed(TestTickHelper.AtMid(1, 10.3));

		Assert.Equal((41L, 10.25, 10.5), entered);
	}

	[Fact]
	public void Feed_UsesMidOfBidAndAsk()
	{
		var tracker = new PriceCellTracker(1.0);
		tracker.Feed(TestTickHelper.AtQuote(0, 9.5, 10.0));

		var entered = tracker.Feed(TestTickHelper.AtQuote(1, 9.75, 10.25));

		Assert.Equal(10L, entered!.Value.Cell);
	}

	[Fact]
	public void Reset_ForgetsCurrentCell()
	{
		var tracker = new PriceCellTracker(1.0);
		tracker.Feed(TestTickHelper.AtMid(0, 10.5));

		tracker.Reset();

		Assert.Null(tracker.Feed(TestTickHelper.AtMid(1, 20.5)));
	}
}
