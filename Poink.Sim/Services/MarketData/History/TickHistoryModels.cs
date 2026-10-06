using System;
using System.Collections.Generic;
using Poink.Sim.Repository;

namespace Poink.Sim.Services.MarketData.History
{
	/// <summary>A tick-history window for one symbol; <see cref="Trigger"/> is recorded against any ticks the API fetch commits.</summary>
	internal sealed record TickHistoryRequest(
		long SymbolId,
		string SymbolName,
		DateTime FromUtc,
		DateTime ToUtc,
		BackfillTrigger Trigger
	);

	/// <summary>
	/// The window split by source: days before today read from <see cref="HistoryTable"/>, today's already-stored
	/// ticks read from <see cref="TodayTable"/>, and only the remainder fetched from the API.
	/// </summary>
	internal sealed record TickHistoryResult(
		IReadOnlyList<Tick> Ticks,
		string HistoryTable,
		string TodayTable,
		int DatabaseTickCount,
		int ApiTickCount,
		bool ApiUnavailable,
		IReadOnlyList<DateOnly> MissingDays
	);

	/// <summary>
	/// Half-open ranges: history [<see cref="HistoryFromUtc"/>, <see cref="HistoryToUtc"/>), stored today
	/// [<see cref="TodayFromUtc"/>, <see cref="ApiFromUtc"/>), API [<see cref="ApiFromUtc"/>, <see cref="ToUtc"/>).
	/// </summary>
	internal readonly record struct TickHistoryPlan(
		DateTime HistoryFromUtc,
		DateTime HistoryToUtc,
		DateTime TodayFromUtc,
		DateTime ApiFromUtc,
		DateTime ToUtc
	)
	{
		public bool HasHistory => HistoryToUtc > HistoryFromUtc;

		public bool HasStoredToday => ApiFromUtc > TodayFromUtc;

		public bool HasApi => ToUtc > ApiFromUtc;
	}
}
