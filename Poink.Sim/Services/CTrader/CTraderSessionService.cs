using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAPI.Net.Auth;
using Poink.CTrader;
using Poink.Sim.Repository;
using Poink.Sim.Services.MarketData;
using CTraderApp = OpenAPI.Net.Auth.App;

namespace Poink.Sim.Services.CTrader
{
	public sealed class CTraderSessionService : ICTraderSessionService, ICTraderHistory, IAsyncDisposable
	{
		/// <summary>cTrader sends prices as integers scaled by 1/100_000 of a unit (e.g. 1.23 -> 123000).</summary>
		private const double PriceScale = 100_000.0;

		/// <summary>Coalesce the per-tick spot stream into at most one UI notification per interval.</summary>
		private const long QuoteNotifyIntervalMs = 200;

		/// <summary>Span of hourly history a chart request loads, kept within cTrader's per-period max range.</summary>
		private const int HistoryDays = 14;

		/// <summary>Upper bound on paged ProtoOAGetTickData requests per side, so one fetch cannot hammer the API unbounded.</summary>
		private const int MaxTickPages = 200;

		/// <summary>
		/// Minimum spacing between historical requests. cTrader caps historical data requests (trendbars and
		/// tick data) at 5 per second per connection; 220ms keeps every such call under that shared budget.
		/// </summary>
		private static readonly TimeSpan HistoricalRequestInterval = TimeSpan.FromMilliseconds(220);

		/// <summary>Poink.Sim has no user accounts; the single dashboard user's token is keyed by this fixed id.</summary>
		private static readonly Guid SingleUserId = new("f6a7b8c9-0d1e-2f3a-4b5c-6d7e8f9a0b1c");

		private readonly CTraderApp _app;
		private readonly CTraderApiOptions _options;
		private readonly IServiceScopeFactory _scopeFactory;
		private readonly ILogger<CTraderSessionService> _logger;
		private readonly SemaphoreSlim _gate = new(1, 1);
		private readonly SemaphoreSlim _historyGate = new(1, 1);
		private readonly SemaphoreSlim _historyRateGate = new(1, 1);
		private readonly object _quotesLock = new();

		private DateTime _lastHistoricalRequestUtc = DateTime.MinValue;
		private readonly Dictionary<long, CTraderSymbolQuote> _quotes = new();

		private CTraderClient? _client;
		private Token? _token;
		private IDisposable? _spotSubscription;
		private long _lastQuoteNotifyTicks;

		public CTraderSessionService(
			CTraderApp app,
			IOptions<CTraderApiOptions> options,
			IServiceScopeFactory scopeFactory,
			ILogger<CTraderSessionService> logger
		)
		{
			_app = app;
			_options = options.Value;
			_scopeFactory = scopeFactory;
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

		public event Action<MarketTick>? TickReceived;

		public Uri? BuildAuthorizationUri() => IsConfigured ? _app.GetAuthUri() : null;

		public async Task CompleteLoginAsync(string authorizationCode, CancellationToken cancellationToken = default)
		{
			await _gate.WaitAsync(cancellationToken);
			try
			{
				await DisconnectAsync();

				SetState(CTraderConnectionState.Connecting, "Exchanging authorization code for a token...");

				var token = await TokenFactory.GetToken(authorizationCode, _app);
				_logger.LogInformation("cTrader: access/refresh token obtained.");

				await SaveTokenAsync(token);

				await ConnectWithTokenAsync(token, cancellationToken);
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

		public async Task TryRestoreAsync()
		{
			await _gate.WaitAsync();
			try
			{
				if (State == CTraderConnectionState.Connected)
					return;

				var token = await LoadTokenAsync();
				if (token is null)
				{
					_logger.LogInformation("cTrader: no saved token to restore.");
					return;
				}

				if (token.ExpiresIn <= DateTimeOffset.UtcNow)
				{
					_logger.LogWarning(
						"cTrader: saved token expired at {ExpiresAt}; a fresh login is required.",
						token.ExpiresIn
					);
					return;
				}

				_logger.LogInformation("cTrader: restoring saved session...");
				SetState(CTraderConnectionState.Connecting, "Restoring saved cTrader session...");

				await ConnectWithTokenAsync(token, CancellationToken.None);
			}
			catch (Exception ex)
			{
				await DisconnectAsync();
				SetState(CTraderConnectionState.Disconnected, "Saved session could not be restored. Log in again.");
				_logger.LogWarning(ex, "cTrader: failed to restore saved session.");
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
				await DeleteTokenAsync();
				SetState(CTraderConnectionState.Disconnected, "Not connected.");
				_logger.LogInformation("cTrader: account disconnected and saved token cleared.");
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
			if (_client is null || SelectedAccountId is null)
				return;

			await _gate.WaitAsync();
			try
			{
				if (Symbols.FirstOrDefault(s => s.SymbolId == symbolId) is not { } info)
					return;

				lock (_quotesLock)
				{
					if (_quotes.ContainsKey(symbolId))
						return;
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
			if (_client is null || SelectedAccountId is null)
				return;

			await _gate.WaitAsync();
			try
			{
				bool wasWatched;
				lock (_quotesLock)
				{
					wasWatched = _quotes.Remove(symbolId);
				}

				if (!wasWatched)
					return;

				await _client.UnsubscribeSpotsAsync(SelectedAccountId.Value, new[] { symbolId });

				_logger.LogInformation("cTrader: stopped watching {SymbolId}.", symbolId);
				Changed?.Invoke();
			}
			finally
			{
				_gate.Release();
			}
		}

		public async Task<IReadOnlyList<CTraderBar>> GetHistoryAsync(
			long symbolId,
			CancellationToken cancellationToken = default
		)
		{
			if (_client is null || SelectedAccountId is null)
				return Array.Empty<CTraderBar>();

			var to = DateTimeOffset.UtcNow;
			var from = to.AddDays(-HistoryDays);

			await ThrottleHistoricalAsync(cancellationToken);
			var result = await _client.GetTrendbarsAsync(
				SelectedAccountId.Value,
				symbolId,
				ProtoOATrendbarPeriod.H1,
				from,
				to,
				cancellationToken
			);

			var bars = result
				.Trendbar.Select(t =>
				{
					var low = t.Low / PriceScale;

					return new CTraderBar(
						DateTimeOffset.FromUnixTimeSeconds(t.UtcTimestampInMinutes * 60L).UtcDateTime,
						low + t.DeltaOpen / PriceScale,
						low + t.DeltaHigh / PriceScale,
						low,
						low + t.DeltaClose / PriceScale,
						t.Volume
					);
				})
				.OrderBy(b => b.TimeUtc)
				.ToList();

			_logger.LogInformation("cTrader: loaded {BarCount} H1 bar(s) for symbol {SymbolId}.", bars.Count, symbolId);

			return bars;
		}

		public bool CanFetchHistory => _client is not null && SelectedAccountId is not null;

		public async Task<CTraderOrderResult> PlaceOrderAsync(
			long symbolId,
			bool isLong,
			double volumeUnits,
			CTraderOrderType orderType,
			double? triggerPrice,
			double? stopLossPrice,
			double? takeProfitPrice,
			CancellationToken cancellationToken = default
		)
		{
			if (_client is null || SelectedAccountId is not { } accountId)
				return new CTraderOrderResult(false, "Not connected.");
			if (volumeUnits <= 0)
				return new CTraderOrderResult(false, "Volume must be greater than zero.");
			if (orderType != CTraderOrderType.Market && triggerPrice is not (> 0))
				return new CTraderOrderResult(false, "A trigger price is required for a Limit/Stop order.");

			var request = new ProtoOANewOrderReq
			{
				CtidTraderAccountId = accountId,
				SymbolId = symbolId,
				OrderType = orderType switch
				{
					CTraderOrderType.Limit => ProtoOAOrderType.Limit,
					CTraderOrderType.Stop => ProtoOAOrderType.Stop,
					_ => ProtoOAOrderType.Market,
				},
				TradeSide = isLong ? ProtoOATradeSide.Buy : ProtoOATradeSide.Sell,
				Volume = (long)Math.Round(volumeUnits * 100),
				ClientOrderId = Guid.NewGuid().ToString("N"),
			};
			if (orderType == CTraderOrderType.Limit)
				request.LimitPrice = triggerPrice!.Value;
			else if (orderType == CTraderOrderType.Stop)
				request.StopPrice = triggerPrice!.Value;
			if (stopLossPrice is { } sl)
				request.StopLoss = sl;
			if (takeProfitPrice is { } tp)
				request.TakeProfit = tp;

			await _gate.WaitAsync(cancellationToken);
			try
			{
				var result = await _client.NewOrderAsync(request, cancellationToken);
				if (result.ExecutionType == ProtoOAExecutionType.OrderRejected)
				{
					_logger.LogWarning(
						"cTrader: order rejected for symbol {SymbolId} -- {ErrorCode}.",
						symbolId,
						result.ErrorCode
					);
					return new CTraderOrderResult(false, $"Rejected: {result.ErrorCode}");
				}

				_logger.LogInformation(
					"cTrader: {Side} order placed for symbol {SymbolId}, {Volume} unit(s).",
					isLong ? "BUY" : "SELL",
					symbolId,
					volumeUnits
				);
				return new CTraderOrderResult(
					true,
					result.ExecutionType == ProtoOAExecutionType.OrderFilled ? "Filled." : "Order accepted."
				);
			}
			catch (CTraderApiException ex)
			{
				_logger.LogError(ex, "cTrader: failed to place order for symbol {SymbolId}.", symbolId);
				return new CTraderOrderResult(false, ex.Message);
			}
			catch (TimeoutException)
			{
				_logger.LogError("cTrader: order placement for symbol {SymbolId} timed out.", symbolId);
				return new CTraderOrderResult(false, "Timed out waiting for a response.");
			}
			finally
			{
				_gate.Release();
			}
		}

		public async Task<CTraderOrderResult> CloseSymbolAsync(long symbolId, CancellationToken cancellationToken = default)
		{
			if (_client is null || SelectedAccountId is not { } accountId)
				return new CTraderOrderResult(false, "Not connected.");

			await _gate.WaitAsync(cancellationToken);
			try
			{
				var reconcile = await _client.ReconcileAsync(accountId, cancellationToken);
				var positions = reconcile.Position.Where(p => p.TradeData.SymbolId == symbolId).ToList();
				var orders = reconcile.Order.Where(o => o.TradeData.SymbolId == symbolId).ToList();

				if (positions.Count == 0 && orders.Count == 0)
					return new CTraderOrderResult(false, "Nothing open for this symbol.");

				var closed = 0;
				var cancelled = 0;
				var errors = new List<string>();

				foreach (var position in positions)
				{
					try
					{
						await _client.ClosePositionAsync(
							accountId,
							position.PositionId,
							position.TradeData.Volume,
							cancellationToken
						);
						closed++;
					}
					catch (CTraderApiException ex)
					{
						errors.Add($"position {position.PositionId}: {ex.Message}");
					}
				}

				foreach (var order in orders)
				{
					try
					{
						await _client.CancelOrderAsync(accountId, order.OrderId, cancellationToken);
						cancelled++;
					}
					catch (CTraderApiException ex)
					{
						errors.Add($"order {order.OrderId}: {ex.Message}");
					}
				}

				_logger.LogInformation(
					"cTrader: closed {ClosedCount} position(s), cancelled {CancelledCount} order(s) for symbol {SymbolId}.",
					closed,
					cancelled,
					symbolId
				);

				return errors.Count == 0
					? new CTraderOrderResult(true, $"Closed {closed} position(s), cancelled {cancelled} order(s).")
					: new CTraderOrderResult(false, string.Join("; ", errors));
			}
			finally
			{
				_gate.Release();
			}
		}

		async Task<IReadOnlyList<Tick>> ICTraderHistory.GetHistoricalTicksAsync(
			long symbolId,
			DateTime fromUtc,
			DateTime toUtc,
			CancellationToken cancellationToken
		)
		{
			if (_client is null || SelectedAccountId is null || toUtc <= fromUtc)
				return Array.Empty<Tick>();

			await _historyGate.WaitAsync(cancellationToken);
			try
			{
				var bid = await FetchSideAsync(symbolId, ProtoOAQuoteType.Bid, fromUtc, toUtc, cancellationToken);
				var ask = await FetchSideAsync(symbolId, ProtoOAQuoteType.Ask, fromUtc, toUtc, cancellationToken);
				var ticks = TickDataDecoder.Merge(bid, ask);

				_logger.LogInformation(
					"cTrader: fetched {TickCount} historical tick(s) for symbol {SymbolId} ({From:u}..{To:u}).",
					ticks.Count,
					symbolId,
					fromUtc,
					toUtc
				);

				return ticks;
			}
			finally
			{
				_historyGate.Release();
			}
		}

		private async Task<List<PricePoint>> FetchSideAsync(
			long symbolId,
			ProtoOAQuoteType type,
			DateTime fromUtc,
			DateTime toUtc,
			CancellationToken cancellationToken
		)
		{
			var points = new List<PricePoint>();
			var windowTo = toUtc;

			for (var page = 0; page < MaxTickPages; page++)
			{
				if (_client is null)
					break;

				await ThrottleHistoricalAsync(cancellationToken);
				var response = await _client.GetTickDataAsync(
					SelectedAccountId!.Value,
					symbolId,
					type,
					fromUtc,
					windowTo,
					cancellationToken
				);
				if (response.TickData.Count == 0)
					break;

				var decoded = TickDataDecoder.Decode(response.TickData);
				points.AddRange(decoded);

				var oldest = decoded[0].TimestampUtc;
				if (!response.HasMore || oldest <= fromUtc)
					break;

				windowTo = oldest.AddMilliseconds(-1);
			}

			points.Sort((a, b) => a.TimestampUtc.CompareTo(b.TimestampUtc));
			return points;
		}

		private async Task ThrottleHistoricalAsync(CancellationToken cancellationToken)
		{
			await _historyRateGate.WaitAsync(cancellationToken);
			try
			{
				var elapsed = DateTime.UtcNow - _lastHistoricalRequestUtc;
				if (elapsed < HistoricalRequestInterval)
					await Task.Delay(HistoricalRequestInterval - elapsed, cancellationToken);

				_lastHistoricalRequestUtc = DateTime.UtcNow;
			}
			finally
			{
				_historyRateGate.Release();
			}
		}

		public async ValueTask DisposeAsync()
		{
			await DisconnectAsync();
			_gate.Dispose();
			_historyGate.Dispose();
			_historyRateGate.Dispose();
		}

		private string EnvironmentLabel => UseLiveEnvironment ? "Live" : "Demo";

		private async Task ConnectWithTokenAsync(Token token, CancellationToken cancellationToken)
		{
			_token = token;

			SetState(CTraderConnectionState.Connecting, $"Connecting ({EnvironmentLabel})...");

			_client = new CTraderClient(_app, UseLiveEnvironment);
			await _client.ConnectAsync(cancellationToken);
			_logger.LogInformation("cTrader: application authorized ({Environment}).", EnvironmentLabel);

			var accounts = await _client.GetAccountsAsync(token.AccessToken, cancellationToken);
			Accounts = accounts
				.CtidTraderAccount.Select(a => new CTraderAccountInfo(
					(long)a.CtidTraderAccountId,
					a.TraderLogin,
					a.IsLive
				))
				.ToList();
			_logger.LogInformation("cTrader: connected with {AccountCount} account(s).", Accounts.Count);

			if (Accounts.FirstOrDefault() is { } account)
			{
				await AuthorizeAndLoadSymbolsAsync(account.AccountId, cancellationToken);
				SetState(
					CTraderConnectionState.Connected,
					$"Connected. {Symbols.Count} symbol(s) on account {account.Login}."
				);
			}
			else
			{
				SetState(CTraderConnectionState.Connected, "Connected, but no trading accounts were returned.");
			}
		}

		private async Task SaveTokenAsync(Token token)
		{
			try
			{
				using var scope = _scopeFactory.CreateScope();
				var store = scope.ServiceProvider.GetRequiredService<ICTraderTokenStore>();
				await store.SaveAsync(SingleUserId, token);
				_logger.LogInformation("cTrader: token persisted.");
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "cTrader: failed to persist token; the session continues without persistence.");
			}
		}

		private async Task<Token?> LoadTokenAsync()
		{
			using var scope = _scopeFactory.CreateScope();
			var store = scope.ServiceProvider.GetRequiredService<ICTraderTokenStore>();
			return await store.GetAsync(SingleUserId);
		}

		private async Task DeleteTokenAsync()
		{
			try
			{
				using var scope = _scopeFactory.CreateScope();
				var store = scope.ServiceProvider.GetRequiredService<ICTraderTokenStore>();
				await store.DeleteAsync(SingleUserId);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "cTrader: failed to clear the persisted token.");
			}
		}

		private async Task AuthorizeAndLoadSymbolsAsync(long accountId, CancellationToken cancellationToken)
		{
			await _client!.AuthorizeAccountAsync(accountId, _token!.AccessToken, cancellationToken);
			SelectedAccountId = accountId;
			_logger.LogInformation("cTrader: account {AccountId} authorized.", accountId);

			await LoadSymbolsAsync(accountId, cancellationToken);

			_spotSubscription = _client.Messages.OfType<ProtoOASpotEvent>().Subscribe(OnSpotEvent);
		}

		public async Task<IReadOnlyList<CTraderSymbolInfo>> RefreshSymbolsAsync(
			CancellationToken cancellationToken = default
		)
		{
			if (_client is null || SelectedAccountId is not { } accountId)
				throw new InvalidOperationException("Not connected to cTrader.");

			await _gate.WaitAsync(cancellationToken);
			try
			{
				return await LoadSymbolsAsync(accountId, cancellationToken);
			}
			finally
			{
				_gate.Release();
			}
		}

		public async Task<ProtoOASymbol?> GetSymbolMetadataAsync(long symbolId, CancellationToken cancellationToken = default)
		{
			if (_client is null || SelectedAccountId is not { } accountId)
				throw new InvalidOperationException("Not connected to cTrader.");

			var response = await _client.GetSymbolByIdAsync(accountId, symbolId, cancellationToken);
			var symbol = response.Symbol.FirstOrDefault();

			_logger.LogInformation(
				"cTrader: metadata {Result} for symbol {SymbolId}.",
				symbol is null ? "not found" : "loaded",
				symbolId
			);

			return symbol;
		}

		private async Task<IReadOnlyList<CTraderSymbolInfo>> LoadSymbolsAsync(
			long accountId,
			CancellationToken cancellationToken
		)
		{
			var symbols = await _client!.GetSymbolsAsync(accountId, cancellationToken: cancellationToken);
			Symbols = symbols
				.Symbol.Select(s => new CTraderSymbolInfo(
					s.SymbolId,
					s.SymbolName,
					s.Enabled,
					s.BaseAssetId,
					s.QuoteAssetId,
					s.SymbolCategoryId,
					s.Description,
					s.SortingNumber
				))
				.OrderBy(s => s.SymbolName)
				.ToList();

			_logger.LogInformation(
				"cTrader: {SymbolCount} symbol(s) loaded for account {AccountId}.",
				Symbols.Count,
				accountId
			);

			return Symbols;
		}

		private void OnSpotEvent(ProtoOASpotEvent spot)
		{
			bool updated = false;
			MarketTick? tick = null;

			lock (_quotesLock)
			{
				if (_quotes.TryGetValue(spot.SymbolId, out var quote))
				{
					var next = quote with
					{
						Bid = spot.HasBid ? spot.Bid / PriceScale : quote.Bid,
						Ask = spot.HasAsk ? spot.Ask / PriceScale : quote.Ask,
						UpdatedAt = DateTime.Now,
					};
					_quotes[spot.SymbolId] = next;
					updated = true;

					if (next.Bid is { } bid && next.Ask is { } ask)
					{
						var timestampUtc = spot.HasTimestamp
							? DateTimeOffset.FromUnixTimeMilliseconds(spot.Timestamp).UtcDateTime
							: DateTime.UtcNow;
						tick = new MarketTick(spot.SymbolId, next.SymbolName, timestampUtc, bid, ask);
					}
				}
			}

			if (tick is { } t)
				TickReceived?.Invoke(t);
			if (updated)
				NotifyQuotesThrottled();
		}

		private void NotifyQuotesThrottled()
		{
			var now = Environment.TickCount64;

			if (now - Interlocked.Read(ref _lastQuoteNotifyTicks) < QuoteNotifyIntervalMs)
				return;

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
