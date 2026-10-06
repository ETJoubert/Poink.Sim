using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Poink.Sim.Repository;

namespace Poink.Sim.Services.CTrader
{
	/// <summary>
	/// Internal history fetch over the authorized cTrader connection, kept off the public
	/// <see cref="ICTraderSessionService"/> because it returns the internal <see cref="Tick"/> type.
	/// Implemented by the session, which owns the connection and the selected account.
	/// </summary>
	internal interface ICTraderHistory
	{
		bool CanFetchHistory { get; }

		/// <summary>Whether fetched history comes from the live environment, which decides its tick table.</summary>
		bool UseLiveEnvironment { get; }

		/// <summary>The account history is fetched for; a change mid-fetch means the result belongs to the previous account.</summary>
		long? SelectedAccountId { get; }

		Task<IReadOnlyList<Tick>> GetHistoricalTicksAsync(
			long symbolId,
			DateTime fromUtc,
			DateTime toUtc,
			CancellationToken cancellationToken = default
		);
	}
}
