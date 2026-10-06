using Poink.Sim.Components.Emulator;
using Xunit;

namespace Poink.Sim.Tests.Components.Pages.Emulator;

public class EmulatorStepperComponentTests
{
	[Theory]
	[InlineData(3, 1, 4)]
	[InlineData(3, -1, 2)]
	[InlineData(0, -1, 0)]
	[InlineData(10, 1, 10)]
	public void Stepped_MovesOneStepWithinBounds(double value, int direction, double expected)
	{
		Assert.Equal(expected, EmulatorStepperComponent.Stepped(value, direction, 1, 0, 10));
	}

	[Fact]
	public void Stepped_DoesNotDriftOnFractionalSteps()
	{
		Assert.Equal(0.3, EmulatorStepperComponent.Stepped(0.2, 1, 0.1, 0, 1));
	}

	[Fact]
	public void Stepped_AnUnreadableValueStepsFromTheMinimum()
	{
		Assert.Equal(1_001, EmulatorStepperComponent.Stepped(double.NaN, 1, 1_000, 1, 1_000_000));
	}
}
