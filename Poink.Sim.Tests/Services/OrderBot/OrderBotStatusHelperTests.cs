using Poink.Sim.Services.CTrader;
using Poink.Sim.Services.OrderBot;
using Xunit;

namespace Poink.Sim.Tests.Services.OrderBot;

public class OrderBotStatusHelperTests
{
	private static readonly CTraderAccountInfo LiveAccount = new(1, 19646985, true);

	private static OrderBotHealthResult Healthy(long accountNumber = 19646985, bool accepting = true) =>
		new(new OrderBotHealth("OrderBot", accountNumber, true, "Broker", accepting), "Online.");

	[Fact]
	public void Evaluate_ReachableBotOnTheSameAccount_IsOnline()
	{
		var status = OrderBotStatusHelper.Evaluate(Healthy(), LiveAccount);

		Assert.Equal(OrderBotConnectionState.Online, status.State);
	}

	[Fact]
	public void Evaluate_UnreachableBot_IsOfflineWithTheProbeMessage()
	{
		var status = OrderBotStatusHelper.Evaluate(
			new OrderBotHealthResult(null, "OrderBot is not reachable."),
			LiveAccount
		);

		Assert.Equal(OrderBotConnectionState.Offline, status.State);
		Assert.Equal("OrderBot is not reachable.", status.Message);
	}

	[Fact]
	public void Evaluate_BotOnAnotherAccount_IsOffline()
	{
		var status = OrderBotStatusHelper.Evaluate(Healthy(accountNumber: 48626334), LiveAccount);

		Assert.Equal(OrderBotConnectionState.Offline, status.State);
		Assert.Contains("48626334", status.Message);
	}

	[Fact]
	public void Evaluate_BotNotAcceptingCommands_IsOffline()
	{
		var status = OrderBotStatusHelper.Evaluate(Healthy(accepting: false), LiveAccount);

		Assert.Equal(OrderBotConnectionState.Offline, status.State);
	}
}
