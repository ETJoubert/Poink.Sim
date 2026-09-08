using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Poink.Sim.Services.MarketData;

namespace Poink.Sim.Services.CTrader
{
	/// <summary>
	/// Single-user cTrader Open API session for this dashboard: owns the OAuth token and the one live
	/// connection, exposing connection state, the authorized account's symbols, and a live-streaming
	/// watchlist to the UI. The token is held in memory only, so a restart requires logging in again
	/// (no refresh-token persistence yet).
	/// </summary>
	public interface ICTraderSessionService
	{
		CTraderConnectionState State { get; }

		string? StatusMessage { get; }

		IReadOnlyList<CTraderAccountInfo> Accounts { get; }

		long? SelectedAccountId { get; }

		IReadOnlyList<CTraderSymbolInfo> Symbols { get; }

		IReadOnlyList<CTraderSymbolQuote> Watchlist { get; }

		bool UseLiveEnvironment { get; }

		bool IsConfigured { get; }

		event Action? Changed;

		/// <summary>Fires for every watched symbol's merged bid/ask quote, feeding the tick backfill worker.</summary>
		event Action<MarketTick>? TickReceived;

		Uri? BuildAuthorizationUri();

		Task CompleteLoginAsync(string authorizationCode, CancellationToken cancellationToken = default);

		Task TryRestoreAsync();

		Task LogoutAsync();

		/// <summary>Re-fetches the authorized account's symbol list from cTrader and updates <see cref="Symbols"/>. Requires a connected session.</summary>
		Task<IReadOnlyList<CTraderSymbolInfo>> RefreshSymbolsAsync(CancellationToken cancellationToken = default);

		bool IsSymbolWatched(long symbolId);

		Task WatchSymbolAsync(long symbolId);

		Task UnwatchSymbolAsync(long symbolId);

		Task<IReadOnlyList<CTraderBar>> GetHistoryAsync(long symbolId, CancellationToken cancellationToken = default);

		/// <summary>
		/// Submits a new order for the given symbol. <paramref name="triggerPrice"/> is required for
		/// Limit/Stop and ignored for Market; <paramref name="volumeUnits"/> is in plain instrument units
		/// (converted to cTrader's 0.01-of-a-unit Volume field internally).
		/// </summary>
		Task<CTraderOrderResult> PlaceOrderAsync(
			long symbolId,
			bool isLong,
			double volumeUnits,
			CTraderOrderType orderType,
			double? triggerPrice,
			double? stopLossPrice,
			double? takeProfitPrice,
			CancellationToken cancellationToken = default
		);

		/// <summary>Closes every open position and cancels every resting pending order for the given symbol.</summary>
		Task<CTraderOrderResult> CloseSymbolAsync(long symbolId, CancellationToken cancellationToken = default);
	}
}
