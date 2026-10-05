using System;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Services.TrainingEngine;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.TrainingEngine;

public class DecisionMaskTests
{
	private static DecisionMask Mask(DecisionMaskResolutionKind resolution, double seconds = 1) =>
		new(new(resolution, seconds));

	[Fact]
	public void PerBar_DecidesOnlyWhenBarCloses()
	{
		var mask = Mask(DecisionMaskResolutionKind.PerBar);

		Assert.False(mask.ShouldDecide(TestTickHelper.Origin, barJustClosed: false));
		Assert.True(mask.ShouldDecide(TestTickHelper.Origin, barJustClosed: true));
	}

	[Fact]
	public void PerTick_AlwaysDecides()
	{
		var mask = Mask(DecisionMaskResolutionKind.PerTick);

		Assert.True(mask.ShouldDecide(TestTickHelper.Origin, false));
		Assert.True(mask.ShouldDecide(TestTickHelper.Origin, false));
	}

	[Fact]
	public void EverySeconds_WaitsForIntervalSinceLastDecision()
	{
		var mask = Mask(DecisionMaskResolutionKind.EverySeconds, 5);

		Assert.True(mask.ShouldDecide(TestTickHelper.Origin, false));
		Assert.False(mask.ShouldDecide(TestTickHelper.Origin.AddSeconds(3), false));
		Assert.True(mask.ShouldDecide(TestTickHelper.Origin.AddSeconds(5), false));
		Assert.False(mask.ShouldDecide(TestTickHelper.Origin.AddSeconds(9), false));
		Assert.True(mask.ShouldDecide(TestTickHelper.Origin.AddSeconds(10), false));
	}

	[Fact]
	public void OnCellWidthChange_IsNotSupported()
	{
		Assert.Throws<NotSupportedException>(() => Mask(DecisionMaskResolutionKind.OnCellWidthChange));
	}
}
