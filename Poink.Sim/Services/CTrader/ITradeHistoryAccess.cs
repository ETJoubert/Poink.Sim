using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Poink.Sim.Services.CTrader
{
	/// <summary>
	/// The session slice closed-trade history callers need. Narrowed off <see cref="ICTraderSessionService"/>
	/// so callers can't reach watchlist or account/lifecycle members they have no business touching.
	/// </summary>
	internal interface ITradeHistoryAccess
	{
		/// <summary>Fetches every position-closing deal in the given window, newest first.</summary>
		Task<IReadOnlyList<CTraderDealInfo>> GetClosedDealsAsync(
			DateTime fromUtc,
			DateTime toUtc,
			CancellationToken cancellationToken = default
		);
	}
}
