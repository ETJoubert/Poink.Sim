using System;
using Poink.Sim.Helpers;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.Helpers;

public class TickTableRangeHelperTests
{
	private static readonly DateTime Max = TestTickHelper.Origin.AddDays(10);

	[Fact]
	public void DefaultRange_LongTable_OpensOnMostRecentDay()
	{
		Assert.Equal((Max.AddDays(-1), Max), TickTableRangeHelper.DefaultRange(TestTickHelper.Origin, Max));
	}

	[Fact]
	public void DefaultRange_ShortTable_OpensOnWholeTable()
	{
		var min = Max.AddHours(-3);

		Assert.Equal((min, Max), TickTableRangeHelper.DefaultRange(min, Max));
	}

	[Fact]
	public void DefaultRange_UnknownStart_OpensOnMostRecentDay()
	{
		Assert.Equal((Max.AddDays(-1), Max), TickTableRangeHelper.DefaultRange(null, Max));
	}

	[Theory]
	[InlineData(-5, -1, false)]
	[InlineData(-5, 0, true)]
	[InlineData(3, 4, true)]
	[InlineData(10, 12, true)]
	[InlineData(11, 12, false)]
	public void Overlaps_RequiresASharedInstant(int fromDay, int toDay, bool expected)
	{
		var from = TestTickHelper.Origin.AddDays(fromDay);
		var to = TestTickHelper.Origin.AddDays(toDay);

		Assert.Equal(expected, TickTableRangeHelper.Overlaps(from, to, TestTickHelper.Origin, Max));
	}

	[Fact]
	public void Overlaps_OpenEndedTable_AlwaysOverlaps()
	{
		Assert.True(TickTableRangeHelper.Overlaps(DateTime.MinValue, DateTime.MinValue.AddDays(1), null, null));
	}
}
