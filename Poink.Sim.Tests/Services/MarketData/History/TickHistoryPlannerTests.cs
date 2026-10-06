using System;
using System.Linq;
using Poink.Sim.Repository;
using Poink.Sim.Services.MarketData.History;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.Services.MarketData.History;

public class TickHistoryPlannerTests
{
	private static readonly DateTime Monday = TestTickHelper.Origin;
	private static readonly DateTime Wednesday = Monday.AddDays(2);
	private static readonly DateTime Now = Wednesday.AddHours(10);

	[Fact]
	public void Plan_WindowSpanningDays_SplitsHistoryAtTodayStart()
	{
		var plan = TickHistoryPlanner.Plan(Monday.AddHours(12), Now, lastStoredTodayUtc: null);

		Assert.Equal((Monday.AddHours(12), Wednesday), (plan.HistoryFromUtc, plan.HistoryToUtc));
		Assert.False(plan.HasStoredToday);
		Assert.Equal((Wednesday, Now), (plan.ApiFromUtc, plan.ToUtc));
	}

	[Fact]
	public void Plan_StoredTodayTicks_StartsApiJustAfterLatest()
	{
		var lastStored = Wednesday.AddHours(9);

		var plan = TickHistoryPlanner.Plan(Monday, Now, lastStored);

		Assert.True(plan.HasStoredToday);
		Assert.Equal(Wednesday, plan.TodayFromUtc);
		Assert.Equal(lastStored.AddTicks(1), plan.ApiFromUtc);
	}

	[Fact]
	public void Plan_WindowWithinToday_HasNoHistory()
	{
		var plan = TickHistoryPlanner.Plan(Wednesday.AddHours(8), Now, lastStoredTodayUtc: null);

		Assert.False(plan.HasHistory);
		Assert.Equal(Wednesday.AddHours(8), plan.ApiFromUtc);
	}

	[Fact]
	public void Plan_LastStoredBeforeWindowStart_IsIgnored()
	{
		var plan = TickHistoryPlanner.Plan(Wednesday.AddHours(8), Now, Wednesday.AddHours(7));

		Assert.False(plan.HasStoredToday);
		Assert.Equal(Wednesday.AddHours(8), plan.ApiFromUtc);
	}

	[Fact]
	public void Plan_LastStoredAtWindowEnd_NeedsNoApi()
	{
		var plan = TickHistoryPlanner.Plan(Monday, Now, Now);

		Assert.False(plan.HasApi);
	}

	[Fact]
	public void MissingWeekdays_SkipsWeekends()
	{
		var friday = Monday.AddDays(4);
		var ticks = new[] { new Tick(friday.AddHours(3), 1, 1) };

		var missing = TickHistoryPlanner.MissingWeekdays(friday, friday.AddDays(4), ticks);

		Assert.Equal(new[] { DateOnly.FromDateTime(friday.AddDays(3)) }, missing);
	}

	[Fact]
	public void MissingWeekdays_IncludesPartialFirstDay()
	{
		var ticks = new[] { new Tick(Monday.AddDays(1).AddHours(5), 1, 1) };

		var missing = TickHistoryPlanner.MissingWeekdays(Monday.AddHours(20), Wednesday, ticks);

		Assert.Equal(new[] { DateOnly.FromDateTime(Monday) }, missing);
	}

	[Fact]
	public void MissingWeekdays_EmptyRange_ReportsNothing()
	{
		Assert.Empty(TickHistoryPlanner.MissingWeekdays(Wednesday, Wednesday, Enumerable.Empty<Tick>()));
	}
}
