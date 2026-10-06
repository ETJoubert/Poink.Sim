using System;
using Poink.Sim.Emulator;
using Poink.Sim.Emulator.Bots;
using Xunit;

namespace Poink.Sim.Tests.Services.Emulator.Bots;

public class RandomBotStrategyTests
{
	private static readonly EmulatorSettings Settings = new() { OrderRangeTicks = 5, MaxOrderFraction = 0.1 };

	private static BotMarketView View(long buyCapacity, long sellCapacity, params long[] openOrderIds) =>
		new(100, 99.99, 100.01, 0.01, 10_000, 0, 10_000, buyCapacity, sellCapacity, openOrderIds);

	[Fact]
	public void Decide_PlacesOrdersWithinRangeAndCapacity()
	{
		var strategy = new RandomBotStrategy(new Random(7));
		var sawBuy = false;
		var sawSell = false;

		for (var i = 0; i < 500; i++)
		{
			var order = Assert.IsType<PlaceOrderDecision>(strategy.Decide(View(200, 100), Settings));

			Assert.InRange(order.Price, 100 - 5 * 0.01 - 1e-9, 100 + 5 * 0.01 + 1e-9);
			Assert.InRange(order.Volume, 1, order.Side == OrderSide.Buy ? 20 : 10);
			sawBuy |= order.Side == OrderSide.Buy;
			sawSell |= order.Side == OrderSide.Sell;
		}

		Assert.True(sawBuy && sawSell);
	}

	[Fact]
	public void Decide_HoldsWithoutCapacity()
	{
		var strategy = new RandomBotStrategy(new Random(7));

		Assert.IsType<HoldDecision>(strategy.Decide(View(0, 0), Settings));
	}

	[Fact]
	public void Decide_CancelsOneOfItsOwnOrders()
	{
		var strategy = new RandomBotStrategy(new Random(7));

		var cancel = Assert.IsType<CancelOrderDecision>(strategy.Decide(View(200, 200, 41, 42), Settings with { CancelChance = 1 }));

		Assert.Contains(cancel.OrderId, new long[] { 41, 42 });
	}
}
