using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Poink.Sim.Services.CTrader
{
	/// <summary>
	/// The session slice SymbolWatchlistPanel needs: watch/unwatch a symbol and read the live watchlist.
	/// Implemented by the session, narrowed off <see cref="ICTraderSessionService"/> so the watchlist
	/// panel can't reach order placement or account/lifecycle members it has no business touching.
	/// </summary>
	internal interface IWatchlistAccess
	{
		CTraderConnectionState State { get; }

		/// <summary>The account the live watchlist belongs to -- switching accounts empties and re-subscribes it.</summary>
		long? SelectedAccountId { get; }

		IReadOnlyList<CTraderSymbolInfo> Symbols { get; }

		/// <summary>The user's explicitly-picked watchlist, shown in the Trade page's WATCHING panel.</summary>
		IReadOnlyList<CTraderSymbolQuote> Watchlist { get; }

		event Action? Changed;

		bool IsSymbolWatched(long symbolId);

		Task WatchSymbolAsync(long symbolId);

		Task UnwatchSymbolAsync(long symbolId);
	}
}
