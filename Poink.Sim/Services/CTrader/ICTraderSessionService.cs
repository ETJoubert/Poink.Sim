using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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

        Uri? BuildAuthorizationUri();

        Task CompleteLoginAsync(string authorizationCode, CancellationToken cancellationToken = default);

        Task TryRestoreAsync();

        Task LogoutAsync();

        bool IsSymbolWatched(long symbolId);

        Task WatchSymbolAsync(long symbolId);

        Task UnwatchSymbolAsync(long symbolId);

        Task<IReadOnlyList<CTraderBar>> GetHistoryAsync(long symbolId, CancellationToken cancellationToken = default);
    }
}
