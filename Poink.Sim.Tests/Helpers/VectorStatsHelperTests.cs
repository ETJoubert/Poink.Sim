using System;
using Poink.Sim.Helpers;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.Helpers;

public class VectorStatsHelperTests
{
	[Fact]
	public void Vwap_IsCumulativeAndSkipsBarsBeforeAnyVolume()
	{
		(DateTime Time, double Price, double Volume)[] bars =
		[
			(TestTickHelper.Origin, 99, 0),
			(TestTickHelper.Origin.AddMinutes(1), 10, 1),
			(TestTickHelper.Origin.AddMinutes(2), 20, 3),
		];

		var points = VectorStatsHelper.Vwap(bars, bar => bar.Time, bar => bar.Price, bar => bar.Volume);

		Assert.Equal(2, points.Count);
		Assert.Equal(new VectorPoint(TestTickHelper.Origin.AddMinutes(1), 10), points[0]);
		Assert.Equal(17.5, points[1].Value, 9);
	}
}
