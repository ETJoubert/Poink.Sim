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
    }
}
