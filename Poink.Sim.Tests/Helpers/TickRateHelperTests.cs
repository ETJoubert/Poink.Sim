using System;
using Poink.Sim.Helpers;
using Poink.Sim.Repository;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.Helpers;

public class TickRateHelperTests
{
	private static readonly TimeSpan Window = TimeSpan.FromSeconds(5);

	[Fact]
	public void Measure_NoTicks_ReturnsNull()
	{
		Assert.Null(TickRateHelper.Measure(Array.Empty<Tick>(), Window));
	}

	[Fact]
	public void Measure_CountsOnlyTicksInsideTheWindowEndingAtTheNewestTick()
	{
		Tick[] ticks =
		[
			TestTickHelper.AtMid(1, 1.0),
			TestTickHelper.AtMid(5, 1.0),
			TestTickHelper.AtMid(6, 1.0),
			TestTickHelper.AtMid(8, 1.0),
			TestTickHelper.AtMid(10, 1.0),
		];

		var rate = TickRateHelper.Measure(ticks, Window)!.Value;

		Assert.Equal(3 / 5.0, rate.TicksPerSecond, 6);
		Assert.Equal(TestTickHelper.Origin.AddSeconds(10), rate.AsOfUtc);
	}

	[Fact]
	public void Measure_SplitsUpAndDownTicksAgainstTheTickBefore()
	{
		Tick[] ticks =
		[
			TestTickHelper.AtMid(0, 1.0),
			TestTickHelper.AtMid(6, 1.1),
			TestTickHelper.AtMid(7, 1.2),
			TestTickHelper.AtMid(8, 1.1),
			TestTickHelper.AtMid(9, 1.1),
		];

		var rate = TickRateHelper.Measure(ticks, Window)!.Value;

		Assert.Equal(4 / 5.0, rate.TicksPerSecond, 6);
		Assert.Equal(2 / 5.0, rate.UpPerSecond, 6);
		Assert.Equal(1 / 5.0, rate.DownPerSecond, 6);
		Assert.Equal(1 / 5.0, rate.DeltaPerSecond, 6);
	}

	[Fact]
	public void Measure_FirstTickWithoutAPredecessor_HasNoDirection()
	{
		var rate = TickRateHelper.Measure([TestTickHelper.AtMid(0, 1.0)], Window)!.Value;

		Assert.Equal(1 / 5.0, rate.TicksPerSecond, 6);
		Assert.Equal(0, rate.UpPerSecond, 6);
		Assert.Equal(0, rate.DownPerSecond, 6);
	}
}
