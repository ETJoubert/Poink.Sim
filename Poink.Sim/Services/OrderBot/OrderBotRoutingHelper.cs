using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Poink.Sim.Services.CTrader;

namespace Poink.Sim.Services.OrderBot
{
	/// <summary>Decides whether the selected account's trades belong to the OrderBot bridge rather than the Open API.</summary>
	internal static class OrderBotRoutingHelper
	{
		public static bool TryGetBotAccount(
			IAccountAccess accounts,
			OrderBotOptions options,
			[NotNullWhen(true)] out CTraderAccountInfo? account
		)
		{
			account = options.RouteLiveOrders
				? accounts.Accounts.FirstOrDefault(a => a.AccountId == accounts.SelectedAccountId && a.IsLive)
				: null;
			return account is not null;
		}
	}
}
