using Poink.Sim.Services.TrainingEngine;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.TrainingEngine;

public class LiveBarBuilderTests
{
	[Fact]
	public void Feed_WithinBucket_UpdatesCurrentBar()
	{
		var builder = new LiveBarBuilder(60);
		builder.Feed(TestTickHelper.AtMid(0, 10));
		builder.Feed(TestTickHelper.AtMid(30, 12));

		var (current, justClosed) = builder.Feed(TestTickHelper.AtMid(45, 9));

		Assert.Null(justClosed);
		Assert.Equal(TestTickHelper.Origin, current.TimeUtc);
		Assert.Equal(10, current.Open);
		Assert.Equal(12, current.High);
		Assert.Equal(9, current.Low);
		Assert.Equal(9, current.Close);
		Assert.Equal(3, current.Volume);
	}

	[Fact]
	public void Feed_IntoNextBucket_ReturnsClosedBarAndStartsNewOne()
	{
		var builder = new LiveBarBuilder(60);
		builder.Feed(TestTickHelper.AtMid(0, 10));
		builder.Feed(TestTickHelper.AtMid(30, 12));

		var (current, justClosed) = builder.Feed(TestTickHelper.AtMid(61, 11));

		Assert.Equal(12, justClosed!.Close);
		Assert.Equal(2, justClosed.Volume);
		Assert.Equal(TestTickHelper.Origin.AddMinutes(1), current.TimeUtc);
		Assert.Equal(11, current.Open);
		Assert.Equal(1, current.Volume);
	}

	[Fact]
	public void Feed_NonPositiveWidth_FallsBackToOneSecondBuckets()
	{
		var builder = new LiveBarBuilder(0);
		builder.Feed(TestTickHelper.AtMid(0, 10));

		var (_, justClosed) = builder.Feed(TestTickHelper.AtMid(1, 11));

		Assert.NotNull(justClosed);
	}
}
