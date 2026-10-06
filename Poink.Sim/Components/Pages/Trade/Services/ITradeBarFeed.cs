using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>
	/// Live bars for every symbol the Trade page charts or compares: each is loaded once through the database-first tick
	/// history, then kept current from the session's tick stream.
	/// </summary>
	internal interface ITradeBarFeed
	{
		/// <summary>Raised with the symbol id whenever that symbol's bars change; may fire off the render thread.</summary>
		event Action<long>? SeriesChanged;

		/// <summary>Raised after the selected account changes and every held series is dropped, so hosts reload what they show.</summary>
		event Action? Reset;

		TradeSymbolSeries? Get(long symbolId);

		/// <summary>The held series whose today's tick table is <paramref name="table"/>, if any.</summary>
		TradeSymbolSeries? GetByTable(string table);

		bool IsLoading(long symbolId);

		/// <summary>Returns the symbol's series at this bar size, loading its history first unless it is already held.</summary>
		Task<TradeSymbolSeries> LoadAsync(long symbolId, string symbolName, int barSizeSeconds, double pipSize);

		/// <summary>Drops the symbol's held series, so the next load re-reads its history, e.g. after its stored ticks were replaced.</summary>
		void Invalidate(long symbolId);

		/// <summary>Drops every held series not in <paramref name="symbolIds"/>, so unwatched symbols stop aggregating.</summary>
		void RetainOnly(IReadOnlySet<long> symbolIds);
	}
}
