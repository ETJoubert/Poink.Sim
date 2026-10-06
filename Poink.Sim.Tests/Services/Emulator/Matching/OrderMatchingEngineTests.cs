using System.Linq;
using Poink.Sim.Emulator;
using Poink.Sim.Emulator.Matching;
using Xunit;

namespace Poink.Sim.Tests.Services.Emulator.Matching;

public class OrderMatchingEngineTests
{
	private readonly OrderMatchingEngine _engine = new();

	private (RestingOrder? Rested, System.Collections.Generic.IReadOnlyList<EmulatorTrade> Trades) Submit(
		int botId,
		OrderSide side,
		long priceTicks,
		long volume,
		bool immediateOrCancel = false
	) => _engine.Submit(new OrderSubmission(botId, side, priceTicks, volume), immediateOrCancel);

	[Fact]
	public void Submit_NonCrossingOrdersRestBestPriceFirst()
	{
		Submit(1, OrderSide.Buy, 99, 5);
		Submit(2, OrderSide.Buy, 98, 5);
		Submit(3, OrderSide.Sell, 101, 5);
		Submit(4, OrderSide.Sell, 102, 5);

		Assert.Equal([99L, 98], _engine.Levels(OrderSide.Buy, 10).Select(l => l.PriceTicks));
		Assert.Equal([101L, 102], _engine.Levels(OrderSide.Sell, 10).Select(l => l.PriceTicks));
		Assert.Equal((99L, 101L), (_engine.BestBidTicks!.Value, _engine.BestAskTicks!.Value));
	}

	[Fact]
	public void Submit_CrossingOrderFillsAtTheRestingPrice()
	{
		Submit(1, OrderSide.Sell, 101, 5);

		var (rested, trades) = Submit(2, OrderSide.Buy, 105, 5);

		Assert.Null(rested);
		Assert.Equal(new EmulatorTrade(101, 5, BuyBotId: 2, SellBotId: 1), Assert.Single(trades));
		Assert.Equal(0, _engine.OrderCount);
	}

	[Fact]
	public void Submit_FillsTheOldestOrderAtAPriceFirst()
	{
		Submit(1, OrderSide.Sell, 101, 3);
		Submit(2, OrderSide.Sell, 101, 3);

		var (_, trades) = Submit(3, OrderSide.Buy, 101, 4);

		Assert.Equal([(1, 3L), (2, 1L)], trades.Select(t => (t.SellBotId, t.Volume)));
		Assert.Equal(2, Assert.Single(_engine.OrdersOf(2)).Remaining);
	}

	[Fact]
	public void Submit_SweepsLevelsAndRestsTheRemainder()
	{
		Submit(1, OrderSide.Sell, 101, 2);
		Submit(2, OrderSide.Sell, 102, 2);

		var (rested, trades) = Submit(3, OrderSide.Buy, 102, 6);

		Assert.Equal([101L, 102], trades.Select(t => t.PriceTicks));
		Assert.Equal((102L, 2L), (rested!.PriceTicks, rested.Remaining));
		Assert.Equal(102, _engine.BestBidTicks);
		Assert.Null(_engine.BestAskTicks);
	}

	[Fact]
	public void Submit_ImmediateOrCancelDropsTheRemainder()
	{
		Submit(1, OrderSide.Buy, 99, 2);

		var (rested, trades) = Submit(2, OrderSide.Sell, 1, 5, immediateOrCancel: true);

		Assert.Null(rested);
		Assert.Equal(2, Assert.Single(trades).Volume);
		Assert.Equal(0, _engine.OrderCount);
	}

	[Fact]
	public void Submit_CancelsTheBotsOwnRestingOrderInsteadOfTradingWithIt()
	{
		Submit(1, OrderSide.Sell, 101, 5);
		Submit(2, OrderSide.Sell, 102, 5);

		var (rested, trades) = Submit(1, OrderSide.Buy, 102, 3);

		Assert.Equal(new EmulatorTrade(102, 3, BuyBotId: 1, SellBotId: 2), Assert.Single(trades));
		Assert.Null(rested);
		Assert.DoesNotContain(_engine.OrdersOf(1), order => order.Side == OrderSide.Sell);
	}

	[Fact]
	public void Cancel_RemovesTheOrderAndItsEmptyLevel()
	{
		var (rested, _) = Submit(1, OrderSide.Buy, 99, 5);

		Assert.True(_engine.Cancel(rested!.Id));
		Assert.False(_engine.Cancel(rested.Id));
		Assert.Null(_engine.BestBidTicks);
		Assert.Empty(_engine.OrderIdsOf(1));
	}
}
