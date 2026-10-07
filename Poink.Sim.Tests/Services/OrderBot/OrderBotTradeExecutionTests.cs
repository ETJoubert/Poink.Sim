using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Services.OrderBot;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.Services.OrderBot;

public class OrderBotTradeExecutionTests
{
	private const long Login = 19646985;
	private static readonly DateTimeOffset Now = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);

	private readonly FakeOrderBotClient _client = new();

	private OrderBotTradeExecution CreateExecution() =>
		new(
			_client,
			Options.Create(new OrderBotOptions { CommandLifetimeSeconds = 5 }),
			new FixedTimeProvider(Now),
			NullLogger<OrderBotTradeExecution>.Instance
		);

	[Fact]
	public async Task PlaceOrder_SendsAPlaceCommandStampedWithItsExpiry()
	{
		await CreateExecution()
			.PlaceOrderAsync(Login, "#USNDAQ100", false, 0.5, CTraderOrderType.Market, 123, 25100, 24900);

		var command = Assert.Single(_client.Sent);
		Assert.Equal(OrderBotCommandKind.PlaceOrder, command.Kind);
		Assert.Equal(Login, command.AccountNumber);
		Assert.Equal("#USNDAQ100", command.SymbolName);
		Assert.False(command.IsLong);
		Assert.Equal(0.5, command.VolumeInUnits);
		Assert.Equal(OrderBotOrderType.Market, command.OrderType);
		Assert.Null(command.TriggerPrice);
		Assert.Equal(25100, command.StopLossPrice);
		Assert.Equal(24900, command.TakeProfitPrice);
		Assert.Equal(Now.UtcDateTime.AddSeconds(5), command.ExpiresAtUtc);
		Assert.NotEqual(Guid.Empty, command.CommandId);
	}

	[Fact]
	public async Task PlaceOrder_LimitOrder_CarriesItsTriggerPrice()
	{
		await CreateExecution()
			.PlaceOrderAsync(Login, "#USNDAQ100", true, 1, CTraderOrderType.Limit, 24000, null, null);

		var command = Assert.Single(_client.Sent);
		Assert.Equal(OrderBotOrderType.Limit, command.OrderType);
		Assert.Equal(24000, command.TriggerPrice);
	}

	[Theory]
	[InlineData(0, CTraderOrderType.Market, null)]
	[InlineData(1, CTraderOrderType.Limit, null)]
	[InlineData(1, CTraderOrderType.Stop, 0d)]
	public async Task PlaceOrder_InvalidRequest_FailsWithoutSending(
		double volume,
		CTraderOrderType orderType,
		double? trigger
	)
	{
		var result = await CreateExecution()
			.PlaceOrderAsync(Login, "#USNDAQ100", true, volume, orderType, trigger, null, null);

		Assert.False(result.Success);
		Assert.Empty(_client.Sent);
	}

	[Fact]
	public async Task ClosePosition_SendsTheBotsResultBack()
	{
		_client.NextResult = new OrderBotCommandResult(false, "Close rejected: MarketClosed");

		var result = await CreateExecution().ClosePositionAsync(Login, 421447512);

		var command = Assert.Single(_client.Sent);
		Assert.Equal(OrderBotCommandKind.ClosePosition, command.Kind);
		Assert.Equal(421447512, command.PositionId);
		Assert.False(result.Success);
		Assert.Equal("Close rejected: MarketClosed", result.Message);
	}

	[Fact]
	public async Task EveryCommand_GetsItsOwnId()
	{
		var execution = CreateExecution();

		await execution.ClosePositionAsync(Login, 1);
		await execution.ClosePositionAsync(Login, 1);

		Assert.NotEqual(_client.Sent[0].CommandId, _client.Sent[1].CommandId);
	}
}
