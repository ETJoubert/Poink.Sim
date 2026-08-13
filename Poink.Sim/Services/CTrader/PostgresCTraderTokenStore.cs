using Microsoft.EntityFrameworkCore;
using OpenAPI.Net.Auth;
using Poink.CTrader;
using Poink.Sim.Data;
using Poink.Sim.Data.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Poink.Sim.Services.CTrader
{
    public class PostgresCTraderTokenStore : ICTraderTokenStore
    {
        private readonly PoinkSimContext _context;

        public PostgresCTraderTokenStore(PoinkSimContext context)
        {
            _context = context;
        }

        public async Task<Token?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var entity = await _context.CTraderAccountTokens
                .FirstOrDefaultAsync(t => t.UserId == userId && !t.IsDeleted, cancellationToken);

            if (entity is null) return null;

            return new Token
            {
                AccessToken = entity.AccessToken,
                RefreshToken = entity.RefreshToken,
                TokenType = entity.TokenType,
                ExpiresIn = entity.ExpiresAtUtc
            };
        }

        public async Task SaveAsync(Guid userId, Token token, CancellationToken cancellationToken = default)
        {
            var entity = await _context.CTraderAccountTokens
                .FirstOrDefaultAsync(t => t.UserId == userId && !t.IsDeleted, cancellationToken);

            if (entity is null)
            {
                entity = new CTraderAccountToken { Id = Guid.NewGuid(), UserId = userId };
                _context.CTraderAccountTokens.Add(entity);
            }

            entity.AccessToken = token.AccessToken;
            entity.RefreshToken = token.RefreshToken;
            entity.TokenType = token.TokenType;
            entity.ExpiresAtUtc = token.ExpiresIn.UtcDateTime;
            entity.ChangedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var entity = await _context.CTraderAccountTokens
                .FirstOrDefaultAsync(t => t.UserId == userId && !t.IsDeleted, cancellationToken);

            if (entity is null) return;

            entity.IsDeleted = true;
            entity.ChangedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
