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
	public sealed class CTraderSessionService
		: ICTraderSessionService,
			ICTraderHistory,
			IWatchlistAccess,
			ITickStreamAccess,
			ITradeExecutionAccess,
			ITradeHistoryAccess,
			IAccountAccess,
			IAsyncDisposable
	{
		/// <summary>cTrader sends prices as integers scaled by 1/100_000 of a unit (e.g. 1.23 -> 123000).</summary>
		private const double PriceScale = 100_000.0;

		/// <summary>Coalesce the per-tick spot stream into at most one UI notification per interval.</summary>
		private const long QuoteNotifyIntervalMs = 200;

		/// <summary>Span of hourly history a chart request loads, kept within cTrader's per-period max range.</summary>
		private const int HistoryDays = 14;

		/// <summary>Upper bound on paged ProtoOAGetTickData requests per side, so one fetch cannot hammer the API unbounded.</summary>
		private const int MaxTickPages = 200;

		/// <summary>Upper bound on paged ProtoOADealList requests, so one fetch cannot hammer the API unbounded.</summary>
		private const int MaxDealPages = 50;

		/// <summary>Deals requested per ProtoOADealList page.</summary>
		private const int DealPageSize = 100;

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
		private readonly object _positionsLock = new();

		private DateTime _lastHistoricalRequestUtc = DateTime.MinValue;
		private readonly Dictionary<long, CTraderSymbolQuote> _quotes = new();

		/// <summary>Subset of <see cref="_quotes"/> the user explicitly picked -- <see cref="Watchlist"/>'s backing set, separate from symbols only subscribed to price an open position.</summary>
		private readonly HashSet<long> _watchedSymbolIds = new();
		private readonly Dictionary<long, CTraderPositionInfo> _positions = new();

		private CTraderClient? _client;
		private Token? _token;
		private IDisposable? _spotSubscription;
		private IDisposable? _traderUpdatedSubscription;
		private IDisposable? _executionSubscription;
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

		public double? Balance { get; private set; }

		/// <summary>Account leverage as a ratio (e.g. 50 for 1:50), decoded from cTrader's leverageInCents.</summary>
		public double? Leverage { get; private set; }

		public IReadOnlyList<CTraderSymbolInfo> Symbols { get; private set; } = Array.Empty<CTraderSymbolInfo>();

		public IReadOnlyList<CTraderPositionInfo> Positions
		{
			get
			{
				lock (_positionsLock)
				{
					return _positions.Values.OrderBy(p => p.OpenTimeUtc).ToList();
				}
			}
		}

		public IReadOnlyList<CTraderSymbolQuote> Watchlist
		{
			get
			{
				lock (_quotesLock)
				{
					return _quotes.Values.Where(q => _watchedSymbolIds.Contains(q.SymbolId)).OrderBy(q => q.SymbolName).ToList();
				}
			}
		}

		public IReadOnlyList<CTraderSymbolQuote> Quotes
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
				return _watchedSymbolIds.Contains(symbolId);
			}
		}

		public Task WatchSymbolAsync(long symbolId) => SubscribeSymbolAsync(symbolId, markWatched: true);

		// Positions price against Quotes (see EnsurePositionSymbolSubscribedAsync), not Watchlist, so a
		// position's symbol gets a subscription here without joining the user's watchlist.
		private Task EnsurePositionSymbolSubscribedAsync(long symbolId) => SubscribeSymbolAsync(symbolId, markWatched: false);

		private async Task SubscribeSymbolAsync(long symbolId, bool markWatched)
		{
			if (_client is null || SelectedAccountId is null)
				return;

			await _gate.WaitAsync();
			bool needsSubscribe;
			try
			{
				if (Symbols.FirstOrDefault(s => s.SymbolId == symbolId) is not { } info)
					return;

				lock (_quotesLock)
				{
					needsSubscribe = !_quotes.ContainsKey(symbolId);
					if (markWatched)
						_watchedSymbolIds.Add(symbolId);
				}

				if (needsSubscribe)
				{
					await _client.SubscribeSpotsAsync(SelectedAccountId.Value, new[] { symbolId });

					lock (_quotesLock)
					{
						_quotes[symbolId] = new CTraderSymbolQuote(symbolId, info.SymbolName, null, null, null);
					}

					_logger.LogInformation("cTrader: watching {Symbol} ({SymbolId}).", info.SymbolName, symbolId);
				}

				Changed?.Invoke();
			}
			finally
			{
				_gate.Release();
			}

			if (needsSubscribe)
				await SeedLatestQuoteAsync(symbolId);
		}

		// The spot subscription above only pushes on the next live tick, leaving Bid/Ask (and positions
		// priced from them) null until one arrives -- backfill from recent tick history instead of waiting.
		private async Task SeedLatestQuoteAsync(long symbolId)
		{
			if (_client is null || SelectedAccountId is null)
				return;

			var toUtc = DateTime.UtcNow;
			var fromUtc = toUtc.AddDays(-HistoryDays);

			double? bid,
				ask;
			try
			{
				await ThrottleHistoricalAsync(CancellationToken.None);
				var bidResponse = await _client.GetTickDataAsync(SelectedAccountId.Value, symbolId, ProtoOAQuoteType.Bid, fromUtc, toUtc);
				var bidPoints = TickDataDecoder.Decode(bidResponse.TickData);
				bid = bidPoints.Count > 0 ? bidPoints[^1].Price : null;

				await ThrottleHistoricalAsync(CancellationToken.None);
				var askResponse = await _client.GetTickDataAsync(SelectedAccountId.Value, symbolId, ProtoOAQuoteType.Ask, fromUtc, toUtc);
				var askPoints = TickDataDecoder.Decode(askResponse.TickData);
				ask = askPoints.Count > 0 ? askPoints[^1].Price : null;
			}
			catch (CTraderApiException ex)
			{
				_logger.LogWarning(ex, "cTrader: failed to seed an initial quote for symbol {SymbolId}.", symbolId);
				return;
			}

			if (bid is null && ask is null)
				return;

			lock (_quotesLock)
			{
				if (!_quotes.TryGetValue(symbolId, out var quote) || quote.Bid is not null || quote.Ask is not null)
					return;

				_quotes[symbolId] = quote with { Bid = bid, Ask = ask };
			}

			_logger.LogInformation("cTrader: seeded an initial quote for symbol {SymbolId} from tick history.", symbolId);
			Changed?.Invoke();
		}

		public async Task UnwatchSymbolAsync(long symbolId)
		{
			if (_client is null || SelectedAccountId is null)
				return;

			await _gate.WaitAsync();
			try
			{
				var stillNeeded = HasOpenPosition(symbolId);
				bool wasWatched;
				lock (_quotesLock)
				{
					wasWatched = _watchedSymbolIds.Remove(symbolId);
					if (!stillNeeded)
						_quotes.Remove(symbolId);
				}

				if (!wasWatched)
					return;

				// An open position on this symbol still needs its price stream, so leave the subscription
				// running -- only drop the wire subscription once nothing references it any more.
				if (!stillNeeded)
					await _client.UnsubscribeSpotsAsync(SelectedAccountId.Value, new[] { symbolId });

				_logger.LogInformation("cTrader: stopped watching {SymbolId}.", symbolId);
				Changed?.Invoke();
			}
			finally
			{
				_gate.Release();
			}
		}

		private bool HasOpenPosition(long symbolId)
		{
			lock (_positionsLock)
			{
				return _positions.Values.Any(p => p.SymbolId == symbolId);
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
						_logger.LogError(ex, "cTrader: failed to close position {PositionId} for symbol {SymbolId}.", position.PositionId, symbolId);
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
						_logger.LogError(ex, "cTrader: failed to cancel order {OrderId} for symbol {SymbolId}.", order.OrderId, symbolId);
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

		public async Task<CTraderOrderResult> ClosePositionAsync(long positionId, CancellationToken cancellationToken = default)
		{
			if (_client is null || SelectedAccountId is not { } accountId)
				return new CTraderOrderResult(false, "Not connected.");

			await _gate.WaitAsync(cancellationToken);
			try
			{
				var reconcile = await _client.ReconcileAsync(accountId, cancellationToken);
				if (reconcile.Position.FirstOrDefault(p => p.PositionId == positionId) is not { } position)
					return new CTraderOrderResult(false, "Position not found.");

				await _client.ClosePositionAsync(accountId, position.PositionId, position.TradeData.Volume, cancellationToken);

				_logger.LogInformation("cTrader: closed position {PositionId}.", positionId);
				return new CTraderOrderResult(true, "Position closed.");
			}
			catch (CTraderApiException ex)
			{
				_logger.LogError(ex, "cTrader: failed to close position {PositionId}.", positionId);
				return new CTraderOrderResult(false, ex.Message);
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

		async Task<IReadOnlyList<CTraderDealInfo>> ITradeHistoryAccess.GetClosedDealsAsync(
			DateTime fromUtc,
			DateTime toUtc,
			CancellationToken cancellationToken
		)
		{
			if (_client is null || SelectedAccountId is not { } accountId || toUtc <= fromUtc)
				return Array.Empty<CTraderDealInfo>();

			await _historyGate.WaitAsync(cancellationToken);
			try
			{
				var deals = new List<CTraderDealInfo>();
				var windowTo = toUtc;

				for (var page = 0; page < MaxDealPages; page++)
				{
					await ThrottleHistoricalAsync(cancellationToken);
					var response = await _client.GetDealsAsync(accountId, fromUtc, windowTo, DealPageSize, cancellationToken);
					if (response.Deal.Count == 0)
						break;

					deals.AddRange(
					response.Deal
						.Where(d => d.DealStatus == ProtoOADealStatus.Filled && d.ClosePositionDetail is not null)
						.Select(ToDealInfo)
				);

					var oldest = DateTimeOffset.FromUnixTimeMilliseconds(response.Deal.Min(d => d.ExecutionTimestamp)).UtcDateTime;
					if (!response.HasMore || oldest <= fromUtc)
						break;

					windowTo = oldest.AddMilliseconds(-1);
				}

				deals.Sort((a, b) => b.ExecutionTimeUtc.CompareTo(a.ExecutionTimeUtc));

				_logger.LogInformation(
					"cTrader: fetched {DealCount} closed deal(s) for account {AccountId} ({From:u}..{To:u}).",
					deals.Count,
					accountId,
					fromUtc,
					toUtc
				);

				return deals;
			}
			finally
			{
				_historyGate.Release();
			}
		}

		private CTraderDealInfo ToDealInfo(ProtoOADeal deal)
		{
			var symbolName = Symbols.FirstOrDefault(s => s.SymbolId == deal.SymbolId)?.SymbolName ?? $"Symbol {deal.SymbolId}";
			var close = deal.ClosePositionDetail;
			var closeScale = close is null ? 1.0 : Math.Pow(10, close.MoneyDigits);

			// Only close-position deals reach here (filtered by the caller): TradeSide is the side of the
			// closing execution, the opposite of the position that was open -- a long closes on a Sell.
			return new CTraderDealInfo(
				deal.DealId,
				deal.PositionId,
				deal.SymbolId,
				symbolName,
				deal.TradeSide == ProtoOATradeSide.Sell,
				deal.Volume / 100.0,
				deal.ExecutionPrice,
				DateTimeOffset.FromUnixTimeMilliseconds(deal.ExecutionTimestamp).UtcDateTime,
				close is null ? null : close.GrossProfit / closeScale,
				close is null ? null : close.Commission / closeScale,
				close is null ? null : close.Swap / closeScale
			);
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
			_traderUpdatedSubscription = _client.Messages.OfType<ProtoOATraderUpdatedEvent>().Subscribe(OnTraderUpdated);
			_executionSubscription = _client.Messages.OfType<ProtoOAExecutionEvent>().Subscribe(OnExecutionEvent);

			await RefreshBalanceAsync(accountId, cancellationToken);
			await LoadPositionsAsync(accountId, cancellationToken);
		}

		private async Task RefreshBalanceAsync(long accountId, CancellationToken cancellationToken)
		{
			try
			{
				var response = await _client!.GetTraderAsync(accountId, cancellationToken);
				ApplyTrader(response.Trader);
				_logger.LogInformation("cTrader: balance {Balance} loaded for account {AccountId}.", Balance, accountId);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "cTrader: failed to fetch account balance for {AccountId}.", accountId);
			}
		}

		private void OnTraderUpdated(ProtoOATraderUpdatedEvent e)
		{
			if (e.CtidTraderAccountId != SelectedAccountId)
				return;

			ApplyTrader(e.Trader);
			_logger.LogInformation("cTrader: balance updated to {Balance} for account {AccountId}.", Balance, SelectedAccountId);
		}

		private void ApplyTrader(ProtoOATrader trader)
		{
			Balance = trader.Balance / Math.Pow(10, trader.MoneyDigits);
			Leverage = trader.LeverageInCents > 0 ? trader.LeverageInCents / 100.0 : null;
			Changed?.Invoke();
		}

		private async Task LoadPositionsAsync(long accountId, CancellationToken cancellationToken)
		{
			try
			{
				var reconcile = await _client!.ReconcileAsync(accountId, cancellationToken);
				int count;
				List<long> symbolIds;
				lock (_positionsLock)
				{
					_positions.Clear();
					foreach (var position in reconcile.Position.Where(p => p.PositionStatus == ProtoOAPositionStatus.PositionStatusOpen))
						_positions[position.PositionId] = ToPositionInfo(position);
					count = _positions.Count;
					symbolIds = _positions.Values.Select(p => p.SymbolId).Distinct().ToList();
				}

				_logger.LogInformation("cTrader: {PositionCount} open position(s) loaded for account {AccountId}.", count, accountId);
				Changed?.Invoke();

				foreach (var symbolId in symbolIds)
					SubscribeSymbolForPosition(symbolId);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "cTrader: failed to load open positions for account {AccountId}.", accountId);
			}
		}

		// A position's PROTO_OA_POSITION_STATUS_CREATED shows up for a pending order with no fill yet -
		// only OPEN and CLOSED represent an actual position change worth tracking here.
		private void OnExecutionEvent(ProtoOAExecutionEvent e)
		{
			if (e.Position is not { } position)
				return;

			switch (position.PositionStatus)
			{
				case ProtoOAPositionStatus.PositionStatusOpen:
					lock (_positionsLock)
					{
						_positions[position.PositionId] = ToPositionInfo(position);
					}
					SubscribeSymbolForPosition(position.TradeData.SymbolId);
					break;
				case ProtoOAPositionStatus.PositionStatusClosed:
					lock (_positionsLock)
					{
						_positions.Remove(position.PositionId);
					}
					if (SelectedAccountId is { } accountId)
						_ = RefreshBalanceAsync(accountId, CancellationToken.None);
					break;
				default:
					return;
			}

			_logger.LogInformation("cTrader: position {PositionId} {Status}.", position.PositionId, position.PositionStatus);
			Changed?.Invoke();
		}

		// Fire-and-forget: both call sites above already run inside _gate (held by the connect flow) or
		// off a reactive event callback, so this can't be awaited inline -- errors are logged, not thrown.
		private void SubscribeSymbolForPosition(long symbolId) => _ = SubscribeSymbolForPositionSafeAsync(symbolId);

		private async Task SubscribeSymbolForPositionSafeAsync(long symbolId)
		{
			try
			{
				await EnsurePositionSymbolSubscribedAsync(symbolId);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "cTrader: failed to subscribe symbol {SymbolId} for a position's live price.", symbolId);
			}
		}

		private CTraderPositionInfo ToPositionInfo(ProtoOAPosition position)
		{
			var symbolId = position.TradeData.SymbolId;
			var symbolName = Symbols.FirstOrDefault(s => s.SymbolId == symbolId)?.SymbolName ?? $"Symbol {symbolId}";

			return new CTraderPositionInfo(
				position.PositionId,
				symbolId,
				symbolName,
				position.TradeData.TradeSide == ProtoOATradeSide.Buy,
				position.TradeData.Volume / 100.0,
				position.Price,
				DateTimeOffset.FromUnixTimeMilliseconds(position.TradeData.OpenTimestamp).UtcDateTime,
				position.HasStopLoss ? position.StopLoss : null,
				position.HasTakeProfit ? position.TakeProfit : null,
				position.UsedMargin / Math.Pow(10, position.MoneyDigits)
			);
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

		public async Task<(
			IReadOnlyList<ProtoOASymbolCategory> Categories,
			IReadOnlyList<ProtoOAAssetClass> AssetClasses
		)> GetSymbolCategoriesAsync(CancellationToken cancellationToken = default)
		{
			if (_client is null || SelectedAccountId is not { } accountId)
				throw new InvalidOperationException("Not connected to cTrader.");

			var categoriesRes = await _client.GetSymbolCategoriesAsync(accountId, cancellationToken);
			var assetClassesRes = await _client.GetAssetClassesAsync(accountId, cancellationToken);

			_logger.LogInformation(
				"cTrader: {CategoryCount} symbol categor(y/ies), {AssetClassCount} asset class(es) loaded.",
				categoriesRes.SymbolCategory.Count,
				assetClassesRes.AssetClass.Count
			);

			return (categoriesRes.SymbolCategory, assetClassesRes.AssetClass);
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
			_traderUpdatedSubscription?.Dispose();
			_traderUpdatedSubscription = null;
			_executionSubscription?.Dispose();
			_executionSubscription = null;

			lock (_quotesLock)
			{
				_quotes.Clear();
				_watchedSymbolIds.Clear();
			}

			lock (_positionsLock)
			{
				_positions.Clear();
			}

			Symbols = Array.Empty<CTraderSymbolInfo>();
			SelectedAccountId = null;
			Balance = null;
			Leverage = null;

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
