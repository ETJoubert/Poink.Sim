using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Poink.Sim.Services.CTrader
{
	/// <summary>
	/// DB-first closed-deal history for the Trade page: reads persisted <see cref="CTraderDealInfo"/> rows and
	/// only calls back to cTrader through <see cref="ITradeHistoryAccess"/> when a position has closed since
	/// the last sync, via <see cref="TradeDealHistoryService"/>.
	/// </summary>
	internal interface ITradeHistoryService
	{
		Task<IReadOnlyList<CTraderDealInfo>> GetClosedDealsAsync(
			DateTime fromUtc,
			DateTime toUtc,
			CancellationToken cancellationToken = default
		);
	}
}
