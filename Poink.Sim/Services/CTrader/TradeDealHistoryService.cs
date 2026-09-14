using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Poink.Sim.Repository;

namespace Poink.Sim.Services.CTrader
{
	/// <summary>
	/// Wraps <see cref="ITradeHistoryAccess"/> with a DB-first cache: reads always come from
	/// <see cref="ITradeDealRepository"/>, and cTrader is only re-queried when <see cref="ITradeHistoryAccess.PositionClosed"/>
	/// has fired since the last sync (the "history sentinel").
	/// </summary>
	internal sealed class TradeDealHistoryService : ITradeHistoryService
	{
		private static readonly TimeSpan InitialSyncLookback = TimeSpan.FromDays(30);

		private readonly ITradeHistoryAccess _history;
		private readonly ITradeDealRepository _repository;
		private readonly ILogger<TradeDealHistoryService> _logger;
		private readonly SemaphoreSlim _syncGate = new(1, 1);

		private volatile bool _dirty = true;
		private long? _lastSyncedAccountId;

		public TradeDealHistoryService(
			ITradeHistoryAccess history,
			ITradeDealRepository repository,
			ILogger<TradeDealHistoryService> logger
		)
		{
			_history = history;
			_repository = repository;
			_logger = logger;
			_history.PositionClosed += () => _dirty = true;
		}

		public async Task<IReadOnlyList<CTraderDealInfo>> GetClosedDealsAsync(
			DateTime fromUtc,
			DateTime toUtc,
			CancellationToken cancellationToken = default
		)
		{
			if (_history.SelectedAccountId is not { } accountId)
				return Array.Empty<CTraderDealInfo>();

			await EnsureSyncedAsync(accountId, cancellationToken);

			return await _repository.GetByAccountAndRangeAsync(accountId, fromUtc, toUtc, cancellationToken);
		}

		private async Task EnsureSyncedAsync(long accountId, CancellationToken cancellationToken)
		{
			if (!_dirty && accountId == _lastSyncedAccountId)
				return;

			await _syncGate.WaitAsync(cancellationToken);
			try
			{
				if (!_dirty && accountId == _lastSyncedAccountId)
					return;

				var syncFromUtc =
					await _repository.GetLatestExecutionTimeUtcAsync(accountId, cancellationToken)
					?? DateTime.UtcNow - InitialSyncLookback;

				var fetched = await _history.GetClosedDealsAsync(syncFromUtc, DateTime.UtcNow, cancellationToken);
				await _repository.UpsertManyAsync(accountId, fetched, cancellationToken);

				_dirty = false;
				_lastSyncedAccountId = accountId;

				_logger.LogInformation(
					"Synced {DealCount} closed deal(s) from cTrader for account {AccountId} since {SyncFrom:u}.",
					fetched.Count,
					accountId,
					syncFromUtc
				);
			}
			finally
			{
				_syncGate.Release();
			}
		}
	}
}
