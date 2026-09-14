using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Poink.Sim.Data;
using Poink.Sim.Services.CTrader;

namespace Poink.Sim.Repository;

internal sealed class TradeDealRepository : ITradeDealRepository
{
	private readonly IDbContextFactory<PoinkSimContext> _contextFactory;
	private readonly ILogger<TradeDealRepository> _logger;

	public TradeDealRepository(IDbContextFactory<PoinkSimContext> contextFactory, ILogger<TradeDealRepository> logger)
	{
		_contextFactory = contextFactory;
		_logger = logger;
	}

	public async Task UpsertManyAsync(
		long accountId,
		IReadOnlyList<CTraderDealInfo> deals,
		CancellationToken cancellationToken = default
	)
	{
		if (deals.Count == 0)
			return;

		await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

		var dealIds = deals.Select(d => d.DealId).ToList();
		var existingDealIds = await context
			.TradeDeals.Where(d => dealIds.Contains(d.DealId))
			.Select(d => d.DealId)
			.ToHashSetAsync(cancellationToken);

		var added = 0;
		foreach (var deal in deals)
		{
			if (existingDealIds.Contains(deal.DealId))
				continue;

			context.TradeDeals.Add(ToEntity(accountId, deal));
			added++;
		}

		if (added == 0)
			return;

		await context.SaveChangesAsync(cancellationToken);

		_logger.LogInformation("Persisted {AddedCount} new trade deal(s) for account {AccountId}.", added, accountId);
	}

	public async Task<IReadOnlyList<CTraderDealInfo>> GetByAccountAndRangeAsync(
		long accountId,
		DateTime fromUtc,
		DateTime toUtc,
		CancellationToken cancellationToken = default
	)
	{
		await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

		var entities = await context
			.TradeDeals.Where(d =>
				d.AccountId == accountId && d.ExecutionTimeUtc >= fromUtc && d.ExecutionTimeUtc <= toUtc
			)
			.OrderByDescending(d => d.ExecutionTimeUtc)
			.ToListAsync(cancellationToken);

		_logger.LogInformation(
			"Read {DealCount} trade deal(s) for account {AccountId} ({From:u}..{To:u}).",
			entities.Count,
			accountId,
			fromUtc,
			toUtc
		);

		return entities.Select(ToModel).ToList();
	}

	public async Task<DateTime?> GetLatestExecutionTimeUtcAsync(
		long accountId,
		CancellationToken cancellationToken = default
	)
	{
		await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

		return await context
			.TradeDeals.Where(d => d.AccountId == accountId)
			.OrderByDescending(d => d.ExecutionTimeUtc)
			.Select(d => (DateTime?)d.ExecutionTimeUtc)
			.FirstOrDefaultAsync(cancellationToken);
	}

	private static Data.Entities.TradeDeal ToEntity(long accountId, CTraderDealInfo deal) =>
		new()
		{
			Id = Guid.NewGuid(),
			AccountId = accountId,
			DealId = deal.DealId,
			PositionId = deal.PositionId,
			SymbolId = deal.SymbolId,
			SymbolName = deal.SymbolName,
			IsLong = deal.IsLong,
			Volume = deal.Volume,
			ExecutionPrice = deal.ExecutionPrice,
			ExecutionTimeUtc = deal.ExecutionTimeUtc,
			RealizedPnl = deal.RealizedPnl,
			Commission = deal.Commission,
			Swap = deal.Swap,
		};

	private static CTraderDealInfo ToModel(Data.Entities.TradeDeal entity) =>
		new(
			entity.DealId,
			entity.PositionId,
			entity.SymbolId,
			entity.SymbolName,
			entity.IsLong,
			entity.Volume,
			entity.ExecutionPrice,
			entity.ExecutionTimeUtc,
			entity.RealizedPnl,
			entity.Commission,
			entity.Swap
		);
}
