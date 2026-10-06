using System.Linq;
using Poink.Sim.Emulator;
using Poink.Sim.Emulator.Broker;
using Xunit;

namespace Poink.Sim.Tests.Services.Emulator.Broker;

public class EmulatorBrokerTests
{
	private static EmulatorBroker CreateBroker(int bots = 2, double points = 10_000, double leverage = 2, double fund = 1_000_000)
	{
		var broker = new EmulatorBroker(
			new EmulatorSettings
			{
				StartPrice = 100,
				TickSize = 1,
				Populations = [new PopulationSettings("Test", bots, points)],
				Leverage = leverage,
				BrokerFund = fund,
			}
		);
		broker.BeginStep();
		return broker;
	}

	[Fact]
	public void Place_RestingOrderDoesNotMoveThePrice()
	{
		var broker = CreateBroker();

		Assert.Equal(OrderOutcome.Accepted, broker.Place(broker.Accounts[0], OrderSide.Buy, 105, 5));

		Assert.Equal(100, broker.LastPrice);
		Assert.Equal(105, broker.Book().BestBid);
	}

	[Fact]
	public void Fill_SettlesBothAccountsAndMovesThePrice()
	{
		var broker = CreateBroker();
		var (seller, buyer) = (broker.Accounts[0], broker.Accounts[1]);

		broker.Place(seller, OrderSide.Sell, 101, 10);
		broker.Place(buyer, OrderSide.Buy, 101, 10);

		Assert.Equal(101, broker.LastPrice);
		Assert.Equal((11_010.0, -10L), (seller.Cash, seller.Position));
		Assert.Equal((8_990.0, 10L), (buyer.Cash, buyer.Position));
		Assert.Equal((1L, 10L), (broker.Summary().TradeCount, broker.Summary().TradedVolume));
	}

	[Fact]
	public void Place_ShortSellingWithinLeverageIsAccepted()
	{
		var broker = CreateBroker();

		Assert.Equal(OrderOutcome.Accepted, broker.Place(broker.Accounts[0], OrderSide.Sell, 100, 200));
	}

	[Fact]
	public void Place_RejectsBeyondLeverage()
	{
		var broker = CreateBroker();

		Assert.Equal(OrderOutcome.RejectedMargin, broker.Place(broker.Accounts[0], OrderSide.Buy, 100, 201));
		Assert.Equal(1, broker.Summary().RejectedOrders);
	}

	[Fact]
	public void Place_RestingOrdersCountTowardsMargin()
	{
		var broker = CreateBroker();
		var bot = broker.Accounts[0];

		broker.Place(bot, OrderSide.Buy, 90, 150);

		Assert.Equal(OrderOutcome.RejectedMargin, broker.Place(bot, OrderSide.Buy, 90, 60));
		Assert.Equal(OrderOutcome.Accepted, broker.Place(bot, OrderSide.Buy, 90, 50));
	}

	[Fact]
	public void Place_RejectsWhenTheFundCannotLend()
	{
		var broker = CreateBroker(fund: 500);
		var bot = broker.Accounts[0];

		Assert.Equal(OrderOutcome.Accepted, broker.Place(bot, OrderSide.Buy, 100, 10));
		Assert.Equal(OrderOutcome.RejectedFund, broker.Place(bot, OrderSide.Buy, 100, 150));
	}

	[Fact]
	public void Cancel_OnlyCancelsTheBotsOwnOrders()
	{
		var broker = CreateBroker();
		var (owner, other) = (broker.Accounts[0], broker.Accounts[1]);
		broker.Place(owner, OrderSide.Buy, 99, 5);
		var orderId = broker.ViewFor(owner).OpenOrderIds[0];

		Assert.False(broker.Cancel(other, orderId));
		Assert.True(broker.Cancel(owner, orderId));
		Assert.Equal(0, broker.Summary().OpenOrders);
	}

	[Fact]
	public void RunStopOuts_ClosesTheLosingPositionAndTheFundAbsorbsTheDeficit()
	{
		var broker = CreateBroker(bots: 3, points: 1_000, leverage: 10);
		var (shorter, longer, maker) = (broker.Accounts[0], broker.Accounts[1], broker.Accounts[2]);
		broker.Place(shorter, OrderSide.Sell, 100, 50);
		broker.Place(longer, OrderSide.Buy, 100, 50);
		broker.Place(maker, OrderSide.Sell, 131, 60);
		broker.Place(maker, OrderSide.Sell, 130, 1);
		broker.Place(longer, OrderSide.Buy, 130, 1);

		broker.RunStopOuts();

		Assert.True(shorter.IsBust);
		Assert.Equal((0L, 0.0, 1), (shorter.Position, shorter.Cash, shorter.StopOuts));
		Assert.Equal(550, broker.Summary().AbsorbedLosses);
		Assert.Equal(1_000_000 - 550, broker.Summary().FundBalance);
		Assert.False(longer.IsBust);
	}

	[Fact]
	public void PopulationSummaries_TotalEachPopulation()
	{
		var broker = CreateBroker();
		broker.Place(broker.Accounts[0], OrderSide.Sell, 101, 10);
		broker.Place(broker.Accounts[1], OrderSide.Buy, 101, 10);

		var summary = Assert.Single(broker.PopulationSummaries());

		Assert.Equal((2, 2, 20_000.0), (summary.BotCount, summary.ActiveBots, summary.Capital));
		Assert.Equal((10L, 10L, 2), (summary.LongUnits, summary.ShortUnits, summary.Fills));
		Assert.Equal(20_000, summary.Equity);
	}

	[Fact]
	public void BotSummaries_ShowEachBotsBalanceAndEquity()
	{
		var broker = CreateBroker();
		broker.Place(broker.Accounts[0], OrderSide.Sell, 101, 10);
		broker.Place(broker.Accounts[1], OrderSide.Buy, 101, 10);
		broker.Place(broker.Accounts[0], OrderSide.Buy, 95, 3);

		var bots = broker.BotSummaries();

		Assert.Equal([1, 2], bots.Select(bot => bot.Id));
		Assert.Equal((-10L, 101.0, 10_000.0, 1), (bots[0].Position, bots[0].AverageEntryPrice, bots[0].Balance, bots[0].OpenOrders));
		Assert.Equal((10L, 10_000.0, 10_000.0), (bots[1].Position, bots[1].Balance, bots[1].Equity));
	}

	[Fact]
	public void UpdateTerms_LoweringLeverageStopsOutPositionsThatNoLongerFit()
	{
		var broker = CreateBroker(bots: 3, points: 1_000, leverage: 10);
		var (longer, seller, maker) = (broker.Accounts[0], broker.Accounts[1], broker.Accounts[2]);
		broker.Place(seller, OrderSide.Sell, 100, 80);
		broker.Place(longer, OrderSide.Buy, 100, 80);
		broker.Place(maker, OrderSide.Buy, 99, 100);

		broker.RunStopOuts();
		Assert.Equal(80, longer.Position);

		broker.UpdateTerms(new EmulatorSettings
		{
			StartPrice = 100,
			TickSize = 1,
			Populations = [new PopulationSettings("Test", 3, 1_000)],
			Leverage = 1,
		});
		broker.RunStopOuts();

		Assert.Equal((0L, 1), (longer.Position, longer.StopOuts));
		Assert.Equal(1, broker.Summary().Leverage);
	}

	[Fact]
	public void Replenish_PaysEveryBotItsStartingCapitalAndRevivesTheBust()
	{
		var broker = CreateBroker(bots: 3, points: 1_000, leverage: 10);
		var (shorter, longer, maker) = (broker.Accounts[0], broker.Accounts[1], broker.Accounts[2]);
		broker.Place(shorter, OrderSide.Sell, 100, 50);
		broker.Place(longer, OrderSide.Buy, 100, 50);
		broker.Place(maker, OrderSide.Sell, 131, 60);
		broker.Place(maker, OrderSide.Sell, 130, 1);
		broker.Place(longer, OrderSide.Buy, 130, 1);
		broker.RunStopOuts();
		Assert.True(shorter.IsBust);

		var paidIn = broker.Replenish();

		Assert.Equal(3_000, paidIn);
		Assert.False(shorter.IsBust);
		Assert.Equal((1_000.0, 2_000.0), (shorter.Cash, shorter.Capital));
		Assert.Equal(6_000, broker.PopulationSummaries()[0].Capital);
	}

	[Fact]
	public void Replenish_OneBotLeavesTheOthersAlone()
	{
		var broker = CreateBroker();

		var paidIn = broker.Replenish(broker.Accounts[1].Id);

		Assert.Equal(10_000, paidIn);
		Assert.Equal((10_000.0, 20_000.0), (broker.Accounts[0].Capital, broker.Accounts[1].Capital));
		Assert.Equal(0, broker.Replenish(999));
	}

	[Fact]
	public void TakeTradedRange_ReportsTheLowAndHighFillsSinceTheLastCall()
	{
		var broker = CreateBroker(bots: 3);
		broker.Place(broker.Accounts[0], OrderSide.Sell, 101, 1);
		broker.Place(broker.Accounts[0], OrderSide.Sell, 104, 1);
		broker.Place(broker.Accounts[1], OrderSide.Buy, 104, 2);

		Assert.Equal((101.0, 104.0), broker.TakeTradedRange());
		Assert.Null(broker.TakeTradedRange());
	}

	[Fact]
	public void ReplenishFund_AddsTheFundSettingOnTopOfTheBalance()
	{
		var broker = CreateBroker(fund: 500);

		var paidIn = broker.ReplenishFund();

		Assert.Equal(500, paidIn);
		Assert.Equal((1_000.0, 1_000.0), (broker.Summary().FundBalance, broker.Summary().FundCapital));
	}

	[Fact]
	public void ReplenishFund_UsesTheFundSettingInForceNow()
	{
		var broker = CreateBroker(fund: 500);
		broker.UpdateTerms(new EmulatorSettings
		{
			StartPrice = 100,
			TickSize = 1,
			Populations = [new PopulationSettings("Test", 2, 10_000)],
			BrokerFund = 2_000,
		});

		broker.ReplenishFund();

		Assert.Equal(2_500, broker.Summary().FundBalance);
	}
}
