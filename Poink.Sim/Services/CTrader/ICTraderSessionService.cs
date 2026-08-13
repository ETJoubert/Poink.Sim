using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Poink.Sim.Services.CTrader
{
    /// <summary>
    /// Single-user cTrader Open API session for this dashboard: owns the OAuth token and the one live
    /// connection, exposing connection state to the UI. The token is held in memory only, so a restart
    /// requires logging in again (no refresh-token persistence yet).
    /// </summary>
    public interface ICTraderSessionService
    {
        CTraderConnectionState State { get; }

        string? StatusMessage { get; }

        IReadOnlyList<CTraderAccountInfo> Accounts { get; }

        bool UseLiveEnvironment { get; }

        bool IsConfigured { get; }

        event Action? Changed;

        Uri? BuildAuthorizationUri();

        Task CompleteLoginAsync(string authorizationCode, CancellationToken cancellationToken = default);

        Task LogoutAsync();
    }
}
