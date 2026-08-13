using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAPI.Net.Auth;
using Poink.CTrader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CTraderApp = OpenAPI.Net.Auth.App;

namespace Poink.Sim.Services.CTrader
{
    public sealed class CTraderSessionService : ICTraderSessionService, IAsyncDisposable
    {
        private readonly CTraderApp _app;
        private readonly CTraderApiOptions _options;
        private readonly ILogger<CTraderSessionService> _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);

        private CTraderClient? _client;
        private Token? _token;

        public CTraderSessionService(CTraderApp app, IOptions<CTraderApiOptions> options, ILogger<CTraderSessionService> logger)
        {
            _app = app;
            _options = options.Value;
            _logger = logger;
        }

        public CTraderConnectionState State { get; private set; } = CTraderConnectionState.Disconnected;

        public string? StatusMessage { get; private set; } = "Not connected.";

        public IReadOnlyList<CTraderAccountInfo> Accounts { get; private set; } = Array.Empty<CTraderAccountInfo>();

        public bool UseLiveEnvironment => _options.UseLiveEnvironment;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_app.ClientId)
            && !string.IsNullOrWhiteSpace(_app.Secret)
            && !string.IsNullOrWhiteSpace(_app.RedirectUri);

        public event Action? Changed;

        public Uri? BuildAuthorizationUri() => IsConfigured ? _app.GetAuthUri() : null;

        public async Task CompleteLoginAsync(string authorizationCode, CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                await DisconnectAsync();

                SetState(CTraderConnectionState.Connecting, "Exchanging authorization code for a token...");

                _token = await TokenFactory.GetToken(authorizationCode, _app);
                _logger.LogInformation("cTrader: access/refresh token obtained.");

                SetState(CTraderConnectionState.Connecting, $"Connecting ({EnvironmentLabel})...");

                _client = new CTraderClient(_app, UseLiveEnvironment);
                await _client.ConnectAsync(cancellationToken);
                _logger.LogInformation("cTrader: application authorized ({Environment}).", EnvironmentLabel);

                var accounts = await _client.GetAccountsAsync(_token.AccessToken, cancellationToken);
                Accounts = accounts.CtidTraderAccount
                    .Select(a => new CTraderAccountInfo((long)a.CtidTraderAccountId, a.TraderLogin, a.IsLive))
                    .ToList();

                SetState(CTraderConnectionState.Connected, $"Connected. {Accounts.Count} trading account(s) available.");
                _logger.LogInformation("cTrader: connected with {AccountCount} account(s).", Accounts.Count);
            }
            catch (Exception ex)
            {
                await DisconnectAsync();
                SetState(CTraderConnectionState.Failed, $"Login failed: {ex.Message}");
                _logger.LogError(ex, "cTrader: login failed.");
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task LogoutAsync()
        {
            await _gate.WaitAsync();
            try
            {
                await DisconnectAsync();
                SetState(CTraderConnectionState.Disconnected, "Not connected.");
                _logger.LogInformation("cTrader: account disconnected.");
            }
            finally
            {
                _gate.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync();
            _gate.Dispose();
        }

        private string EnvironmentLabel => UseLiveEnvironment ? "Live" : "Demo";

        private async Task DisconnectAsync()
        {
            if (_client is not null)
            {
                await _client.DisposeAsync();
                _client = null;
            }

            _token = null;
            Accounts = Array.Empty<CTraderAccountInfo>();
        }

        private void SetState(CTraderConnectionState state, string message)
        {
            State = state;
            StatusMessage = message;
            Changed?.Invoke();
        }
    }
}
