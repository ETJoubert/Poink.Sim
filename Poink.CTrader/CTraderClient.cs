using System.Reactive.Subjects;
using Google.Protobuf;
using OpenAPI.Net;
using OpenAPI.Net.Auth;
using OpenAPI.Net.Helpers;

namespace Poink.CTrader
{
    /// <inheritdoc cref="ICTraderClient"/>
    public sealed class CTraderClient : ICTraderClient
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

        private readonly App _app;
        private readonly Mode _mode;
        private readonly Subject<IMessage> _messages = new();

        private OpenClient? _client;
        private IDisposable? _forwardingSubscription;

        public CTraderClient(App app, bool useLiveEnvironment = false)
        {
            _app = app;
            _mode = useLiveEnvironment ? Mode.Live : Mode.Demo;
        }

        public IObservable<IMessage> Messages => _messages;

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_client is not null) throw new InvalidOperationException("Already connected.");

            var host = ApiInfo.GetHost(_mode);

            _client = new OpenClient(host, ApiInfo.Port, TimeSpan.FromSeconds(10));
            _forwardingSubscription = _client.Subscribe(_messages.OnNext, _messages.OnError);

            await _client.Connect();

            await SendAndWaitAsync<ProtoOAApplicationAuthRes>(new ProtoOAApplicationAuthReq
            {
                ClientId = _app.ClientId,
                ClientSecret = _app.Secret
            }, cancellationToken);
        }

        public Task<ProtoOAGetAccountListByAccessTokenRes> GetAccountsAsync(string accessToken, CancellationToken cancellationToken = default) =>
            SendAndWaitAsync<ProtoOAGetAccountListByAccessTokenRes>(new ProtoOAGetAccountListByAccessTokenReq
            {
                AccessToken = accessToken
            }, cancellationToken);

        public Task<ProtoOAAccountAuthRes> AuthorizeAccountAsync(long ctidTraderAccountId, string accessToken, CancellationToken cancellationToken = default) =>
            SendAndWaitAsync<ProtoOAAccountAuthRes>(new ProtoOAAccountAuthReq
            {
                CtidTraderAccountId = ctidTraderAccountId,
                AccessToken = accessToken
            }, cancellationToken);

        public Task<ProtoOASymbolsListRes> GetSymbolsAsync(long ctidTraderAccountId, bool includeArchivedSymbols = false, CancellationToken cancellationToken = default) =>
            SendAndWaitAsync<ProtoOASymbolsListRes>(new ProtoOASymbolsListReq
            {
                CtidTraderAccountId = ctidTraderAccountId,
                IncludeArchivedSymbols = includeArchivedSymbols
            }, cancellationToken);

        public Task<ProtoOASymbolByIdRes> GetSymbolByIdAsync(long ctidTraderAccountId, long symbolId, CancellationToken cancellationToken = default)
        {
            var request = new ProtoOASymbolByIdReq { CtidTraderAccountId = ctidTraderAccountId };
            request.SymbolId.Add(symbolId);

            return SendAndWaitAsync<ProtoOASymbolByIdRes>(request, cancellationToken);
        }

        public Task<ProtoOASubscribeSpotsRes> SubscribeSpotsAsync(long ctidTraderAccountId, IEnumerable<long> symbolIds, CancellationToken cancellationToken = default)
        {
            var request = new ProtoOASubscribeSpotsReq { CtidTraderAccountId = ctidTraderAccountId };
            request.SymbolId.AddRange(symbolIds);

            return SendAndWaitAsync<ProtoOASubscribeSpotsRes>(request, cancellationToken);
        }

        public Task<ProtoOAUnsubscribeSpotsRes> UnsubscribeSpotsAsync(long ctidTraderAccountId, IEnumerable<long> symbolIds, CancellationToken cancellationToken = default)
        {
            var request = new ProtoOAUnsubscribeSpotsReq { CtidTraderAccountId = ctidTraderAccountId };
            request.SymbolId.AddRange(symbolIds);

            return SendAndWaitAsync<ProtoOAUnsubscribeSpotsRes>(request, cancellationToken);
        }

        public Task<ProtoOAGetTrendbarsRes> GetTrendbarsAsync(long ctidTraderAccountId, long symbolId, ProtoOATrendbarPeriod period, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) =>
            SendAndWaitAsync<ProtoOAGetTrendbarsRes>(new ProtoOAGetTrendbarsReq
            {
                CtidTraderAccountId = ctidTraderAccountId,
                SymbolId = symbolId,
                Period = period,
                FromTimestamp = from.ToUnixTimeMilliseconds(),
                ToTimestamp = to.ToUnixTimeMilliseconds()
            }, cancellationToken);

        public Task<ProtoOAGetTickDataRes> GetTickDataAsync(long ctidTraderAccountId, long symbolId, ProtoOAQuoteType type, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) =>
            SendAndWaitAsync<ProtoOAGetTickDataRes>(new ProtoOAGetTickDataReq
            {
                CtidTraderAccountId = ctidTraderAccountId,
                SymbolId = symbolId,
                Type = type,
                FromTimestamp = from.ToUnixTimeMilliseconds(),
                ToTimestamp = to.ToUnixTimeMilliseconds()
            }, cancellationToken);

        public Task<ProtoOAExecutionEvent> NewOrderAsync(ProtoOANewOrderReq request, CancellationToken cancellationToken = default) =>
            SendAndWaitAsync<ProtoOAExecutionEvent>(
                request,
                e => IsTerminalOrderEvent(e.ExecutionType) && e.Order?.ClientOrderId == request.ClientOrderId,
                cancellationToken);

        public Task<ProtoOAExecutionEvent> ClosePositionAsync(long ctidTraderAccountId, long positionId, long volume, CancellationToken cancellationToken = default) =>
            SendAndWaitAsync<ProtoOAExecutionEvent>(
                new ProtoOAClosePositionReq
                {
                    CtidTraderAccountId = ctidTraderAccountId,
                    PositionId = positionId,
                    Volume = volume
                },
                e => e.Position?.PositionId == positionId,
                cancellationToken);

        public Task<ProtoOAExecutionEvent> CancelOrderAsync(long ctidTraderAccountId, long orderId, CancellationToken cancellationToken = default) =>
            SendAndWaitAsync<ProtoOAExecutionEvent>(
                new ProtoOACancelOrderReq
                {
                    CtidTraderAccountId = ctidTraderAccountId,
                    OrderId = orderId
                },
                e => e.Order?.OrderId == orderId,
                cancellationToken);

        public Task<ProtoOAReconcileRes> ReconcileAsync(long ctidTraderAccountId, CancellationToken cancellationToken = default) =>
            SendAndWaitAsync<ProtoOAReconcileRes>(new ProtoOAReconcileReq
            {
                CtidTraderAccountId = ctidTraderAccountId
            }, cancellationToken);

        public Task<ProtoOATraderRes> GetTraderAsync(long ctidTraderAccountId, CancellationToken cancellationToken = default) =>
            SendAndWaitAsync<ProtoOATraderRes>(new ProtoOATraderReq
            {
                CtidTraderAccountId = ctidTraderAccountId
            }, cancellationToken);

        // A new order's first execution event is usually ACCEPTED before a later FILLED/REJECTED
        // one - only these three are "done", so NewOrderAsync doesn't resolve on an interim event.
        private static bool IsTerminalOrderEvent(ProtoOAExecutionType type) =>
            type is ProtoOAExecutionType.OrderFilled or ProtoOAExecutionType.OrderAccepted or ProtoOAExecutionType.OrderRejected;

        private Task<TRes> SendAndWaitAsync<TRes>(IMessage request, CancellationToken cancellationToken) where TRes : IMessage =>
            SendAndWaitAsync<TRes>(request, static _ => true, cancellationToken);

        private async Task<TRes> SendAndWaitAsync<TRes>(IMessage request, Func<TRes, bool> isMatch, CancellationToken cancellationToken) where TRes : IMessage
        {
            if (_client is null) throw new InvalidOperationException("Not connected yet - call ConnectAsync first.");

            var tcs = new TaskCompletionSource<TRes>(TaskCreationOptions.RunContinuationsAsynchronously);

            using var subscription = _messages.Subscribe(
                message =>
                {
                    switch (message)
                    {
                        case TRes typed when isMatch(typed):
                            tcs.TrySetResult(typed);
                            break;
                        case ProtoOAErrorRes error:
                            tcs.TrySetException(new CTraderApiException(error.ErrorCode, error.Description));
                            break;
                    }
                },
                ex => tcs.TrySetException(ex));

            await _client.SendMessage(request);

            using var timeoutCts = new CancellationTokenSource(DefaultTimeout);
            using var timeoutRegistration = timeoutCts.Token.Register(() =>
                tcs.TrySetException(new TimeoutException($"Timed out waiting for {typeof(TRes).Name}.")));
            using var cancellationRegistration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

            return await tcs.Task;
        }

        public ValueTask DisposeAsync()
        {
            _forwardingSubscription?.Dispose();
            _client?.Dispose();
            _client = null;

            _messages.OnCompleted();
            _messages.Dispose();

            return ValueTask.CompletedTask;
        }
    }
}
