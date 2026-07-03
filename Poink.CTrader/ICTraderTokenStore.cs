using OpenAPI.Net.Auth;

namespace Poink.CTrader
{
    public interface ICTraderTokenStore
    {
        Task<Token?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

        Task SaveAsync(Guid userId, Token token, CancellationToken cancellationToken = default);

        Task DeleteAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
