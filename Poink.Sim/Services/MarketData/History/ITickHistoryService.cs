using System;
using System.Threading;
using System.Threading.Tasks;

namespace Poink.Sim.Services.MarketData.History
{
	/// <summary>
	/// Database-first tick history: days before today come from the tick tables (filled by the Data page import),
	/// and only today's not-yet-stored ticks are fetched from the API -- and committed, so the next read is free.
	/// </summary>
	internal interface ITickHistoryService
	{
		Task<TickHistoryResult> LoadAsync(TickHistoryRequest request, CancellationToken cancellationToken = default);

		/// <summary>Fetches and commits today's ticks the database is missing up to <paramref name="upToUtc"/>; returns the count fetched.</summary>
		Task<int> CatchUpTodayAsync(
			long symbolId,
			string symbolName,
			DateTime upToUtc,
			BackfillTrigger trigger,
			CancellationToken cancellationToken = default
		);
	}
}
