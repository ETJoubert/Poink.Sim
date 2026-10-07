using System;
using System.Text.Json;
using cAlgo.Robots;
using Poink.Sim.Services.OrderBot;
using Xunit;

namespace Poink.Sim.Tests.Services.OrderBot;

/// <summary>Round-trips JSON between Poink.Sim's models and the bot's own contract file, compiled in from cBots/OrderBot.</summary>
public class OrderBotContractTests
{
	[Fact]
	public void Command_SentByPoinkSim_ReadsBackIntactOnTheBot()
	{
		var sent = new OrderBotCommand(
			Guid.NewGuid(),
			19646985,
			OrderBotCommandKind.PlaceOrder,
			new DateTime(2026, 10, 7, 9, 0, 5, DateTimeKind.Utc),
			"#USNDAQ100",
			true,
			0.5,
			OrderBotOrderType.Stop,
			25000,
			24900,
			25200,
			null
		);

		var received = JsonSerializer.Deserialize<BotCommand>(
			JsonSerializer.Serialize(sent, OrderBotClient.JsonOptions),
			OrderBotClient.JsonOptions
		)!;

		Assert.Equal(sent.CommandId, received.CommandId);
		Assert.Equal(sent.AccountNumber, received.AccountNumber);
		Assert.Equal(BotCommandKind.PlaceOrder, received.Kind);
		Assert.Equal(sent.ExpiresAtUtc, received.ExpiresAtUtc);
		Assert.Equal(DateTimeKind.Utc, received.ExpiresAtUtc.Kind);
		Assert.Equal(sent.SymbolName, received.SymbolName);
		Assert.True(received.IsLong);
		Assert.Equal(sent.VolumeInUnits, received.VolumeInUnits);
		Assert.Equal(BotOrderType.Stop, received.OrderType);
		Assert.Equal(sent.TriggerPrice, received.TriggerPrice);
		Assert.Equal(sent.StopLossPrice, received.StopLossPrice);
		Assert.Equal(sent.TakeProfitPrice, received.TakeProfitPrice);
		Assert.Null(received.PositionId);
	}

	[Fact]
	public void Result_SentByTheBot_ReadsBackIntactInPoinkSim()
	{
		var sent = new BotCommandResult(true, "Filled.", 421447512, null);

		var received = JsonSerializer.Deserialize<OrderBotCommandResult>(
			JsonSerializer.Serialize(sent, OrderBotClient.JsonOptions),
			OrderBotClient.JsonOptions
		);

		Assert.Equal(new OrderBotCommandResult(true, "Filled.", 421447512, null), received);
	}

	[Fact]
	public void Health_SentByTheBot_ReadsBackIntactInPoinkSim()
	{
		var sent = new BotHealth("OrderBot", 19646985, true, "Broker", true);

		var received = JsonSerializer.Deserialize<OrderBotHealth>(
			JsonSerializer.Serialize(sent, OrderBotClient.JsonOptions),
			OrderBotClient.JsonOptions
		);

		Assert.Equal(new OrderBotHealth("OrderBot", 19646985, true, "Broker", true), received);
	}
}
