using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Repository;

namespace Poink.Sim.Services.MarketData.History
{
	/// <summary>Splits a tick-history window into database and API ranges so only today's unstored ticks cost an API call.</summary>
	internal static class TickHistoryPlanner
	{
		public static DateTime DayStartUtc(DateTime utc) => DateTime.SpecifyKind(utc.Date, DateTimeKind.Utc);

		/// <summary>Plans the window, starting the API range just after <paramref name="lastStoredTodayUtc"/> when today already has stored ticks.</summary>
		public static TickHistoryPlan Plan(DateTime fromUtc, DateTime toUtc, DateTime? lastStoredTodayUtc)
		{
			var todayStart = DayStartUtc(toUtc);
			var historyTo = Max(fromUtc, Min(toUtc, todayStart));
			var todayFrom = Max(fromUtc, todayStart);

			var apiFrom = lastStoredTodayUtc is { } last && last >= todayFrom ? last.AddTicks(1) : todayFrom;

			return new TickHistoryPlan(fromUtc, historyTo, todayFrom, Min(apiFrom, toUtc), toUtc);
		}

		/// <summary>Mon-Fri days overlapping [<paramref name="fromUtc"/>, <paramref name="toUtc"/>) with no tick among <paramref name="ticks"/>.</summary>
		public static IReadOnlyList<DateOnly> MissingWeekdays(DateTime fromUtc, DateTime toUtc, IEnumerable<Tick> ticks)
		{
			if (toUtc <= fromUtc)
				return Array.Empty<DateOnly>();

			var present = ticks.Select(t => DateOnly.FromDateTime(t.Timestamp)).ToHashSet();
			var missing = new List<DateOnly>();
			var lastDay = DateOnly.FromDateTime(toUtc.AddTicks(-1));

			for (var day = DateOnly.FromDateTime(fromUtc); day <= lastDay; day = day.AddDays(1))
			{
				if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || present.Contains(day))
					continue;
				missing.Add(day);
			}

			return missing;
		}

		/// <summary>Distinct UTC days the ticks fall on, so each touched day's stats can be refreshed once.</summary>
		public static IReadOnlyList<DateOnly> DaysSpanned(IEnumerable<Tick> ticks) =>
			ticks.Select(t => DateOnly.FromDateTime(t.Timestamp)).Distinct().Order().ToList();

		private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

		private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
	}
}
