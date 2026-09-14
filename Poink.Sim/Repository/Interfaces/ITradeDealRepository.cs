using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Poink.Sim.Services.CTrader;

namespace Poink.Sim.Repository;

internal interface ITradeDealRepository
{
	/// <summary>Persists any deal in the batch not already stored, keyed by DealId. Existing deals are left untouched, since a settled deal never changes.</summary>
	Task UpsertManyAsync(long accountId, IReadOnlyList<CTraderDealInfo> deals, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<CTraderDealInfo>> GetByAccountAndRangeAsync(
		long accountId,
		DateTime fromUtc,
		DateTime toUtc,
		CancellationToken cancellationToken = default
	);

	/// <summary>The most recent deal's execution time stored for this account, or null when none are stored yet.</summary>
	Task<DateTime?> GetLatestExecutionTimeUtcAsync(long accountId, CancellationToken cancellationToken = default);
}
