using Poink.Sim.Services.CTrader;

namespace Poink.Sim.Services.OrderBot
{
	/// <summary>Turns a health probe into the bridge status shown on the Trade page for the selected live account.</summary>
	internal static class OrderBotStatusHelper
	{
		public static OrderBotStatus Evaluate(OrderBotHealthResult probe, CTraderAccountInfo account)
		{
			if (probe.Health is not { } health)
				return new OrderBotStatus(OrderBotConnectionState.Offline, probe.Message);
			if (health.AccountNumber != account.Login)
				return new OrderBotStatus(
					OrderBotConnectionState.Offline,
					$"OrderBot is attached to account {health.AccountNumber}, not {account.Login}."
				);
			if (!health.AcceptingCommands)
				return new OrderBotStatus(
					OrderBotConnectionState.Offline,
					"OrderBot is running but not accepting commands. Enable AcceptExternalCommands and AllowLive."
				);

			return new OrderBotStatus(
				OrderBotConnectionState.Online,
				$"OrderBot ready on {health.BrokerName} account {health.AccountNumber}."
			);
		}
	}
}
