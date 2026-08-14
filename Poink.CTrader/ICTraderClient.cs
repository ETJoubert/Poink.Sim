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
    }
}
