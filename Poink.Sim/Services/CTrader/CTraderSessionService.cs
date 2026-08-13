using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAPI.Net.Auth;
using Poink.CTrader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using CTraderApp = OpenAPI.Net.Auth.App;

namespace Poink.Sim.Services.CTrader
{
    public sealed class CTraderSessionService : ICTraderSessionService, IAsyncDisposable
    {
        /// <summary>cTrader sends prices as integers scaled by 1/100_000 of a unit (e.g. 1.23 -> 123000).</summary>
        private const double PriceScale = 100_000.0;

        /// <summary>Coalesce the per-tick spot stream into at most one UI notification per interval.</summary>
        private const long QuoteNotifyIntervalMs = 200;

        /// <summary>Span of hourly history a chart request loads, kept within cTrader's per-period max range.</summary>
        private const int HistoryDays = 14;

        private readonly CTraderApp _app;
        private readonly CTraderApiOptions _options;
        private readonly ILogger<CTraderSessionService> _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly object _quotesLock = new();
        private readonly Dictionary<long, CTraderSymbolQuote> _quotes = new();

        private CTraderClient? _client;
        private Token? _token;
        private IDisposable? _spotSubscription;
        private long _lastQuoteNotifyTicks;

        public CTraderSessionService(CTraderApp app, IOptions<CTraderApiOptions> options, ILogger<CTraderSessionService> logger)
        {
            _app = app;
            _options = options.Value;
            _logger = logger;
        }

        public CTraderConnectionState State { get; private set; } = CTraderConnectionState.Disconnected;

        public string? StatusMessage { get; private set; } = "Not connected.";

        public IReadOnlyList<CTraderAccountInfo> Accounts { get; private set; } = Array.Empty<CTraderAccountInfo>();

        public long? SelectedAccountId { get; private set; }

        public IReadOnlyList<CTraderSymbolInfo> Symbols { get; private set; } = Array.Empty<CTraderSymbolInfo>();

        public IReadOnlyList<CTraderSymbolQuote> Watchlist
        {
            get
            {
                lock (_quotesLock)
                {
                    return _quotes.Values.OrderBy(q => q.SymbolName).ToList();
                }
            }
        }

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
                _logger.LogInformation("cTrader: connected with {AccountCount} account(s).", Accounts.Count);

                if (Accounts.FirstOrDefault() is { } account)
                {
                    await AuthorizeAndLoadSymbolsAsync(account.AccountId, cancellationToken);
                    SetState(CTraderConnectionState.Connected,
                        $"Connected. {Symbols.Count} symbol(s) on account {account.Login}.");
                }
                else
                {
                    SetState(CTraderConnectionState.Connected, "Connected, but no trading accounts were returned.");
                }
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

        public bool IsSymbolWatched(long symbolId)
        {
            lock (_quotesLock)
            {
                return _quotes.ContainsKey(symbolId);
            }
        }

        public async Task WatchSymbolAsync(long symbolId)
        {
            if (_client is null || SelectedAccountId is null) return;

            await _gate.WaitAsync();
            try
            {
                if (Symbols.FirstOrDefault(s => s.SymbolId == symbolId) is not { } info) return;

                lock (_quotesLock)
                {
                    if (_quotes.ContainsKey(symbolId)) return;
                }

                await _client.SubscribeSpotsAsync(SelectedAccountId.Value, new[] { symbolId });

                lock (_quotesLock)
                {
                    _quotes[symbolId] = new CTraderSymbolQuote(symbolId, info.SymbolName, null, null, null);
                }

                _logger.LogInformation("cTrader: watching {Symbol} ({SymbolId}).", info.SymbolName, symbolId);
                Changed?.Invoke();
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task UnwatchSymbolAsync(long symbolId)
        {
            if (_client is null || SelectedAccountId is null) return;

            await _gate.WaitAsync();
            try
            {
                bool wasWatched;
                lock (_quotesLock)
                {
                    wasWatched = _quotes.Remove(symbolId);
                }

                if (!wasWatched) return;

                await _client.UnsubscribeSpotsAsync(SelectedAccountId.Value, new[] { symbolId });

                _logger.LogInformation("cTrader: stopped watching {SymbolId}.", symbolId);
                Changed?.Invoke();
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<IReadOnlyList<CTraderBar>> GetHistoryAsync(long symbolId, CancellationToken cancellationToken = default)
        {
            if (_client is null || SelectedAccountId is null) return Array.Empty<CTraderBar>();

            var to = DateTimeOffset.UtcNow;
            var from = to.AddDays(-HistoryDays);

            var result = await _client.GetTrendbarsAsync(
                SelectedAccountId.Value, symbolId, ProtoOATrendbarPeriod.H1, from, to, cancellationToken);

            var bars = result.Trendbar
                .Select(t =>
                {
                    var low = t.Low / PriceScale;

                    return new CTraderBar(
                        DateTimeOffset.FromUnixTimeSeconds(t.UtcTimestampInMinutes * 60L).UtcDateTime,
                        low + t.DeltaOpen / PriceScale,
                        low + t.DeltaHigh / PriceScale,
                        low,
                        low + t.DeltaClose / PriceScale,
                        t.Volume);
                })
                .OrderBy(b => b.TimeUtc)
                .ToList();

            _logger.LogInformation("cTrader: loaded {BarCount} H1 bar(s) for symbol {SymbolId}.", bars.Count, symbolId);

            return bars;
        }

        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync();
            _gate.Dispose();
        }

        private string EnvironmentLabel => UseLiveEnvironment ? "Live" : "Demo";

        private async Task AuthorizeAndLoadSymbolsAsync(long accountId, CancellationToken cancellationToken)
        {
            await _client!.AuthorizeAccountAsync(accountId, _token!.AccessToken, cancellationToken);
            SelectedAccountId = accountId;
            _logger.LogInformation("cTrader: account {AccountId} authorized.", accountId);

            var symbols = await _client.GetSymbolsAsync(accountId, cancellationToken: cancellationToken);
            Symbols = symbols.Symbol
                .Select(s => new CTraderSymbolInfo(s.SymbolId, s.SymbolName, s.Enabled))
                .OrderBy(s => s.SymbolName)
                .ToList();

            _spotSubscription = _client.Messages
                .OfType<ProtoOASpotEvent>()
                .Subscribe(OnSpotEvent);

            _logger.LogInformation("cTrader: {SymbolCount} symbol(s) loaded for account {AccountId}.", Symbols.Count, accountId);
        }

        private void OnSpotEvent(ProtoOASpotEvent spot)
        {
            bool updated = false;

            lock (_quotesLock)
            {
                if (_quotes.TryGetValue(spot.SymbolId, out var quote))
                {
                    _quotes[spot.SymbolId] = quote with
                    {
                        Bid = spot.HasBid ? spot.Bid / PriceScale : quote.Bid,
                        Ask = spot.HasAsk ? spot.Ask / PriceScale : quote.Ask,
                        UpdatedAt = DateTime.Now
                    };
                    updated = true;
                }
            }

            if (updated) NotifyQuotesThrottled();
        }

        private void NotifyQuotesThrottled()
        {
            var now = Environment.TickCount64;

            if (now - Interlocked.Read(ref _lastQuoteNotifyTicks) < QuoteNotifyIntervalMs) return;

            Interlocked.Exchange(ref _lastQuoteNotifyTicks, now);
            Changed?.Invoke();
        }

        private async Task DisconnectAsync()
        {
            _spotSubscription?.Dispose();
            _spotSubscription = null;

            lock (_quotesLock)
            {
                _quotes.Clear();
            }

            Symbols = Array.Empty<CTraderSymbolInfo>();
            SelectedAccountId = null;

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
