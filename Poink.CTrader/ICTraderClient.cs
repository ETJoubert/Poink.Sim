using Google.Protobuf;

namespace Poink.CTrader
{
    /// <summary>
    /// A connected, application-authorized cTrader Open API session. One instance backs one
    /// connection - construct a new instance per connection attempt, it is not meant to be reused
    /// after <see cref="IAsyncDisposable.DisposeAsync"/>.
    /// </summary>
    public interface ICTraderClient : IAsyncDisposable
    {
        /// <summary>Every message received on this connection, including the ones consumed internally by this client.</summary>
        IObservable<IMessage> Messages { get; }

        /// <summary>Opens the connection and completes the application-auth handshake.</summary>
        Task ConnectAsync(CancellationToken cancellationToken = default);

        Task<ProtoOAGetAccountListByAccessTokenRes> GetAccountsAsync(string accessToken, CancellationToken cancellationToken = default);

        Task<ProtoOAAccountAuthRes> AuthorizeAccountAsync(long ctidTraderAccountId, string accessToken, CancellationToken cancellationToken = default);

        /// <summary>Requires <see cref="AuthorizeAccountAsync"/> to have been called for this account first.</summary>
        Task<ProtoOASymbolsListRes> GetSymbolsAsync(long ctidTraderAccountId, bool includeArchivedSymbols = false, CancellationToken cancellationToken = default);

        /// <summary>Requires <see cref="AuthorizeAccountAsync"/> first. The account's small, broker-defined list of symbol categories.</summary>
        Task<ProtoOASymbolCategoryListRes> GetSymbolCategoriesAsync(long ctidTraderAccountId, CancellationToken cancellationToken = default);

        /// <summary>Requires <see cref="AuthorizeAccountAsync"/> first. The asset classes (e.g. Forex, Metals) that <see cref="ProtoOASymbolCategory.AssetClassId"/> references.</summary>
        Task<ProtoOAAssetClassListRes> GetAssetClassesAsync(long ctidTraderAccountId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Requires <see cref="AuthorizeAccountAsync"/> first. Returns the full trading detail for one symbol
        /// (digits, volume/commission/swap rules, schedule, holidays) - unlike <see cref="GetSymbolsAsync"/>'s
        /// lightweight list, this costs one API call per symbol.
        /// </summary>
        Task<ProtoOASymbolByIdRes> GetSymbolByIdAsync(long ctidTraderAccountId, long symbolId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Requires <see cref="AuthorizeAccountAsync"/> to have been called for this account first.
        /// Live price ticks then arrive as <c>ProtoOASpotEvent</c> messages on <see cref="Messages"/> -
        /// this only confirms the subscription was accepted.
        /// </summary>
        Task<ProtoOASubscribeSpotsRes> SubscribeSpotsAsync(long ctidTraderAccountId, IEnumerable<long> symbolIds, CancellationToken cancellationToken = default);

        Task<ProtoOAUnsubscribeSpotsRes> UnsubscribeSpotsAsync(long ctidTraderAccountId, IEnumerable<long> symbolIds, CancellationToken cancellationToken = default);

        /// <summary>
        /// Requires <see cref="AuthorizeAccountAsync"/> to have been called for this account first.
        /// cTrader enforces its own maximum time range per <paramref name="period"/> (e.g. minute bars
        /// cover a shorter span than daily bars) - an out-of-range request surfaces as a <see cref="CTraderApiException"/>.
        /// </summary>
        Task<ProtoOAGetTrendbarsRes> GetTrendbarsAsync(long ctidTraderAccountId, long symbolId, ProtoOATrendbarPeriod period, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

        /// <summary>
        /// Requires <see cref="AuthorizeAccountAsync"/> first. Returns one delta-encoded, newest-first chunk of
        /// historical ticks for a single quote side (bid or ask); page older data by moving <paramref name="to"/>
        /// before the oldest tick received while <c>ProtoOAGetTickDataRes.HasMore</c> is true.
        /// </summary>
        Task<ProtoOAGetTickDataRes> GetTickDataAsync(long ctidTraderAccountId, long symbolId, ProtoOAQuoteType type, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

        /// <summary>
        /// Requires <see cref="AuthorizeAccountAsync"/> first. Submits a new order and waits for the terminal
        /// execution event (accepted for a resting Limit/Stop, filled for Market, or rejected) that matches
        /// <paramref name="request"/>'s ClientOrderId - the caller must set that to a unique value.
        /// </summary>
        Task<ProtoOAExecutionEvent> NewOrderAsync(ProtoOANewOrderReq request, CancellationToken cancellationToken = default);

        /// <summary>Requires <see cref="AuthorizeAccountAsync"/> first. Closes (or partially closes) an open position.</summary>
        Task<ProtoOAExecutionEvent> ClosePositionAsync(long ctidTraderAccountId, long positionId, long volume, CancellationToken cancellationToken = default);

        /// <summary>Requires <see cref="AuthorizeAccountAsync"/> first. Cancels a resting pending order.</summary>
        Task<ProtoOAExecutionEvent> CancelOrderAsync(long ctidTraderAccountId, long orderId, CancellationToken cancellationToken = default);

        /// <summary>Requires <see cref="AuthorizeAccountAsync"/> first. Returns every currently open position and resting pending order on the account.</summary>
        Task<ProtoOAReconcileRes> ReconcileAsync(long ctidTraderAccountId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Requires <see cref="AuthorizeAccountAsync"/> first. Returns the account's current Balance
        /// (scaled by the response's MoneyDigits) and related bonus/leverage detail. Balance changes
        /// also arrive unprompted as <c>ProtoOATraderUpdatedEvent</c> on <see cref="Messages"/>.
        /// </summary>
        Task<ProtoOATraderRes> GetTraderAsync(long ctidTraderAccountId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Requires <see cref="AuthorizeAccountAsync"/> first. Returns one newest-first chunk of closed
        /// deals in the given window; page older data by moving <paramref name="to"/> before the oldest
        /// deal received while <c>ProtoOADealListRes.HasMore</c> is true.
        /// </summary>
        Task<ProtoOADealListRes> GetDealsAsync(long ctidTraderAccountId, DateTimeOffset from, DateTimeOffset to, int? maxRows = null, CancellationToken cancellationToken = default);
    }
}
