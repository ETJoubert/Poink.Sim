using System;
using Poink.Sim.Components.Pages.Trade.Helpers;
using Poink.Sim.Components.Pages.Trade.Models;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.Components.Pages.Trade.Helpers;

public class SymbolCoverageHelperTests
{
	private static readonly DateTime Monday = TestTickHelper.Origin;
	private static readonly DateTime Friday = Monday.AddDays(4);

	[Theory]
	[InlineData(2, 1)]
	[InlineData(7, 4)]
	[InlineData(5, 4)]
	[InlineData(6, 4)]
	public void LastExpectedDayStartUtc_IsPreviousWeekday(int todayOffset, int expectedOffset)
	{
		Assert.Equal(Monday.AddDays(expectedOffset), SymbolCoverageHelper.LastExpectedDayStartUtc(Monday.AddDays(todayOffset).AddHours(10)));
	}

	[Fact]
	public void Build_ReachingLastWeekday_IsCurrent()
	{
		var nextMonday = Monday.AddDays(7).AddHours(9);
		var coverage = SymbolCoverageHelper.Build([("live_ticks_eurusd", 100, Monday, Friday.AddHours(21))], nextMonday);

		Assert.Equal(SymbolCoverageStatus.Current, coverage["live_ticks_eurusd"].Status);
	}

	[Fact]
	public void Build_EndingBeforeLastWeekday_IsStale()
	{
		var coverage = SymbolCoverageHelper.Build([("live_ticks_eurusd", 100, Monday, Monday.AddHours(21))], Friday.AddHours(9));

		Assert.Equal(SymbolCoverageStatus.Stale, coverage["live_ticks_eurusd"].Status);
	}

	[Fact]
	public void Build_EmptyTable_HasNoData()
	{
		var coverage = SymbolCoverageHelper.Build([("live_ticks_eurusd", 0, null, null)], Friday);

		Assert.False(coverage["live_ticks_eurusd"].HasData);
	}

	[Fact]
	public void For_MapsSymbolNameToLiveTable()
	{
		var coverage = SymbolCoverageHelper.Build([("live_ticks_us500", 10, Monday, Monday.AddHours(1))], Monday.AddDays(1));

		Assert.Equal(10, SymbolCoverageHelper.For("US 500", coverage).Rows);
	}

	[Fact]
	public void ShortLabel_ShowsStoredRangeOrNone()
	{
		var coverage = SymbolCoverageHelper.Build([("live_ticks_eurusd", 10, Monday, Friday)], Friday.AddDays(1));

		Assert.Equal("2026-01-05 → 2026-01-09", SymbolCoverageHelper.ShortLabel(SymbolCoverageHelper.For("EURUSD", coverage)));
		Assert.Equal("no stored ticks", SymbolCoverageHelper.ShortLabel(SymbolCoverageHelper.For("GBPUSD", coverage)));
	}

	[Fact]
	public void For_SymbolWithoutTable_HasNoData()
	{
		var coverage = SymbolCoverageHelper.Build([("demo_ticks_eurusd", 10, Monday, Monday.AddHours(1))], Monday.AddDays(1));

		var result = SymbolCoverageHelper.For("EURUSD", coverage);

		Assert.Equal(("live_ticks_eurusd", SymbolCoverageStatus.None), (result.Table, result.Status));
	}
}
