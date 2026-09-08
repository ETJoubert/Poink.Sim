using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Poink.Sim.Data;
using Poink.Sim.Models;
using Poink.Sim.Services.CTrader;

namespace Poink.Sim.Services.Symbols
{
	public sealed class SymbolService : ISymbolService
	{
		private readonly PoinkSimContext _context;
		private readonly ICTraderSessionService _session;
		private readonly ILogger<SymbolService> _logger;

		public SymbolService(PoinkSimContext context, ICTraderSessionService session, ILogger<SymbolService> logger)
		{
			_context = context;
			_session = session;
			_logger = logger;
		}

		public async Task<IReadOnlyList<SymbolModel>> ListAsync(CancellationToken cancellationToken = default)
		{
			var entities = await _context.Symbols.OrderBy(s => s.SymbolName).ToListAsync(cancellationToken);

			return entities.Select(ToModel).ToList();
		}

		public async Task<IReadOnlyList<SymbolModel>> RefreshFromCTraderAsync(
			CancellationToken cancellationToken = default
		)
		{
			_logger.LogInformation("Refreshing symbols from cTrader...");

			var fetched = await _session.RefreshSymbolsAsync(cancellationToken);
			var existing = await _context.Symbols.ToDictionaryAsync(s => s.SymbolId, cancellationToken);
			var now = DateTime.UtcNow;

			foreach (var symbol in fetched)
			{
				if (existing.TryGetValue(symbol.SymbolId, out var entity))
				{
					entity.SymbolName = symbol.SymbolName;
					entity.Enabled = symbol.Enabled;
					entity.BaseAssetId = symbol.BaseAssetId;
					entity.QuoteAssetId = symbol.QuoteAssetId;
					entity.SymbolCategoryId = symbol.SymbolCategoryId;
					entity.Description = symbol.Description;
					entity.SortingNumber = symbol.SortingNumber;
					entity.ChangedAt = now;
				}
				else
				{
					_context.Symbols.Add(
						new Data.Entities.Symbol
						{
							Id = Guid.NewGuid(),
							SymbolId = symbol.SymbolId,
							SymbolName = symbol.SymbolName,
							Enabled = symbol.Enabled,
							BaseAssetId = symbol.BaseAssetId,
							QuoteAssetId = symbol.QuoteAssetId,
							SymbolCategoryId = symbol.SymbolCategoryId,
							Description = symbol.Description,
							SortingNumber = symbol.SortingNumber,
							CreatedAt = now,
						}
					);
				}
			}

			await _context.SaveChangesAsync(cancellationToken);

			_logger.LogInformation("Refreshed {SymbolCount} symbol(s) from cTrader.", fetched.Count);

			return await ListAsync(cancellationToken);
		}

		private static SymbolModel ToModel(Data.Entities.Symbol entity) =>
			new(
				entity.Id,
				entity.SymbolId,
				entity.SymbolName,
				entity.Enabled,
				entity.BaseAssetId,
				entity.QuoteAssetId,
				entity.SymbolCategoryId,
				entity.Description,
				entity.SortingNumber,
				entity.CreatedAt,
				entity.ChangedAt
			);
	}
}
