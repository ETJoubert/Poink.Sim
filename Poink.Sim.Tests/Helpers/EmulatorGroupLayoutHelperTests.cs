using Poink.Sim.Helpers;
using Xunit;

namespace Poink.Sim.Tests.Helpers;

public class EmulatorGroupLayoutHelperTests
{
	[Theory]
	[InlineData(1, 1, 1)]
	[InlineData(4, 1, 4)]
	[InlineData(5, 2, 4)]
	[InlineData(8, 2, 4)]
	[InlineData(9, 2, 5)]
	[InlineData(21, 2, 11)]
	public void GridOf_FillsAColumnOfFourThenWrapsAndGrowsTallerPastTwoColumns(int cards, int columns, int rows)
	{
		Assert.Equal((columns, rows), EmulatorGroupLayoutHelper.GridOf(cards));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(6)]
	[InlineData(17)]
	public void GridOf_AlwaysHasRoomForEveryCard(int cards)
	{
		var (columns, rows) = EmulatorGroupLayoutHelper.GridOf(cards);

		Assert.True(columns * rows >= cards);
	}
}
