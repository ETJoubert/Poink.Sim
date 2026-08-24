using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Poink.Sim.Services.MarketData
{
	/// <summary>
	/// Control surface for the tick backfill worker, consumed by the Data page. Tracking a symbol makes
	/// the worker persist its live ticks (and, once available, backfill the pre-stream gap from the API);
	/// untracking stops persistence. Tables are created only on explicit opt-in.
	/// </summary>
	internal interface ITickBackfillController
	{
		bool IsPaused { get; }

		event Action? Changed;

		IReadOnlyList<SymbolIngestStatus> GetStatuses();

		void Pause();

		void Resume();

		void Track(long symbolId, string symbolName, bool createTable);

		void Untrack(long symbolId);

		void ForceFlush(long symbolId);

		/// <summary>Manually fetches a symbol's ticks over a range via the history source and stores them. Returns rows written.</summary>
		Task<long> BackfillRangeAsync(long symbolId, string symbolName, DateTime fromUtc, DateTime toUtc);
	}
}
