using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Poink.Sim.Services.CTrader
{
	/// <summary>
	/// The session slice the account/connection chrome needs: connection state, account list, and
	/// login/logout. Implemented by the session, narrowed off
	/// <see cref="ICTraderSessionService"/> so callers can't reach watchlist or trade-execution members
	/// they have no business touching.
	/// </summary>
	internal interface IAccountAccess
	{
		CTraderConnectionState State { get; }

		string? StatusMessage { get; }

		IReadOnlyList<CTraderAccountInfo> Accounts { get; }

		long? SelectedAccountId { get; }

		bool IsConfigured { get; }

		event Action? Changed;

		Uri? BuildAuthorizationUri();

		Task LogoutAsync();
	}
}
