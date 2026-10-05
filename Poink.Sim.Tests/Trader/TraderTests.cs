using System.Linq;
using Poink.Sim.Models;
using Poink.Sim.Tests.TestSupport;
using Poink.Sim.Trader.Commission;
using Xunit;

namespace Poink.Sim.Tests.Trader;

using Poink.Sim.Trader;

public class TraderTests
{
	private static readonly System.DateTime Now = TestTickHelper.Origin;

	private static SimulationDecision Long(double fraction, double? stopLoss = null, double? takeProfit = null) =>
		new(SimulationAction.EnterLong, fraction, stopLoss, takeProfit);

	private static SimulationDecision Short(double fraction, double? stopLoss = null, double? takeProfit = null) =>
		new(SimulationAction.EnterShort, fraction, stopLoss, takeProfit);

	private static readonly SimulationDecision CloseDecision = new(SimulationAction.Close);

	[Fact]
	public void Apply_MarketLong_FillsAtAskWithLeveragedSize()
	{
		var trader = new Trader(10_000, new TraderSettings(Leverage: 10));

		var execution = trader.Apply(Long(0.5), 1.24, 1.25, Now);

		Assert.NotNull(execution);
		Assert.Equal(SimulationAction.EnterLong, execution.Value.Action);
		Assert.Equal(PositionDirection.Long, trader.CurrentPosition!.Value.Direction);
		Assert.Equal(1.25, trader.CurrentPosition.Value.EntryPrice);
		Assert.Equal(40_000, trader.CurrentPosition.Value.Size, 6);
		Assert.Equal(5_000, trader.MarginUsed, 6);
	}

	[Fact]
	public void Apply_MarketShort_FillsAtBid()
	{
		var trader = new Trader(1000);

		trader.Apply(Short(1), 99, 100, Now);

		Assert.Equal(99, trader.CurrentPosition!.Value.EntryPrice);
	}

	[Fact]
	public void Apply_CapitalFractionAboveOne_IsClampedToFullCash()
	{
		var trader = new Trader(1000);

		trader.Apply(Long(5), 100, 100, Now);

		Assert.Equal(10, trader.CurrentPosition!.Value.Size, 9);
	}

	[Fact]
	public void Apply_ZeroCapitalFraction_OpensNothing()
	{
		var trader = new Trader(1000);

		var execution = trader.Apply(Long(0), 100, 100, Now);

		Assert.Null(execution);
		Assert.Null(trader.CurrentPosition);
	}

	[Fact]
	public void Apply_EntryWhilePositionOpen_IsIgnored()
	{
		var trader = new Trader(1000);
		trader.Apply(Long(0.5), 100, 100, Now);

		var second = trader.Apply(Short(0.5), 100, 100, Now);

		Assert.Null(second);
		Assert.Equal(PositionDirection.Long, trader.CurrentPosition!.Value.Direction);
	}

	[Fact]
	public void Close_ProfitableLong_CreditsCashAndLogsTrade()
	{
		var trader = new Trader(1000);
		trader.Apply(Long(1), 99, 100, Now);

		var execution = trader.Apply(CloseDecision, 109, 110, Now.AddMinutes(1));

		Assert.Equal(90, execution!.Value.RealizedPnl!.Value, 6);
		Assert.Equal(1090, trader.Cash, 6);
		Assert.Equal(90, trader.RealizedPnl, 6);
		Assert.Null(trader.CurrentPosition);
		var trade = Assert.Single(trader.TradeLog);
		Assert.Equal(100, trade.EntryPrice);
		Assert.Equal(109, trade.ExitPrice);
		Assert.Equal(TradeCloseReason.Manual, trade.CloseReason);
	}

	[Fact]
	public void Close_ProfitableShort_ExitsAtAsk()
	{
		var trader = new Trader(1000);
		trader.Apply(Short(1), 100, 101, Now);

		trader.Apply(CloseDecision, 89, 90, Now);

		Assert.Equal(100, trader.TradeLog.Single().RealizedPnl, 6);
		Assert.Equal(90, trader.TradeLog.Single().ExitPrice);
	}

	[Fact]
	public void Commission_IsChargedOnOpenAndCloseLegs()
	{
		var settings = new TraderSettings(CommissionModel: new PercentOfNotionalCommission(1));
		var trader = new Trader(1000, settings);

		var open = trader.Apply(Long(1), 2, 2, Now);
		double cashAfterOpen = trader.Cash;
		trader.Apply(CloseDecision, 2, 2, Now);

		Assert.Equal(10, open!.Value.Commission, 9);
		Assert.Equal(990, cashAfterOpen, 9);
		Assert.Equal(980, trader.Cash, 9);
		Assert.Equal(-20, trader.RealizedPnl, 9);
		var trade = trader.TradeLog.Single();
		Assert.Equal(20, trade.Commission, 9);
		Assert.Equal(-20, trade.RealizedPnl, 9);
	}

	[Fact]
	public void Advance_LongStopLossBreached_ClosesWithStopLossReason()
	{
		var trader = new Trader(1000);
		trader.Apply(Long(1, stopLoss: 95), 100, 100, Now);

		var result = trader.Advance(95, 95.1, Now.AddSeconds(1));

		var fill = Assert.Single(result.AutomaticFills);
		Assert.Equal(SimulationAction.Close, fill.Action);
		Assert.Null(trader.CurrentPosition);
		Assert.Equal(TradeCloseReason.StopLoss, trader.TradeLog.Single().CloseReason);
		Assert.Equal(-50, trader.TradeLog.Single().RealizedPnl, 9);
	}

	[Fact]
	public void Advance_LongAboveStopLoss_KeepsPositionOpen()
	{
		var trader = new Trader(1000);
		trader.Apply(Long(1, stopLoss: 95), 100, 100, Now);

		var result = trader.Advance(95.01, 95.1, Now.AddSeconds(1));

		Assert.Empty(result.AutomaticFills);
		Assert.NotNull(trader.CurrentPosition);
	}

	[Fact]
	public void Advance_ShortTakeProfitReached_ClosesWithTakeProfitReason()
	{
		var trader = new Trader(1000);
		trader.Apply(Short(1, takeProfit: 90), 100, 100, Now);

		trader.Advance(89.9, 90, Now.AddSeconds(1));

		Assert.Null(trader.CurrentPosition);
		Assert.Equal(TradeCloseReason.TakeProfit, trader.TradeLog.Single().CloseReason);
	}

	[Fact]
	public void Advance_MarginLevelBelowStopOut_ForceClosesPosition()
	{
		var trader = new Trader(1000, new TraderSettings(Leverage: 100, StopOutLevelPercent: 50));
		trader.Apply(Long(1), 100, 100, Now);

		trader.Advance(99.6, 99.6, Now.AddSeconds(1));
		Assert.NotNull(trader.CurrentPosition);

		trader.Advance(99.4, 99.4, Now.AddSeconds(2));

		Assert.Null(trader.CurrentPosition);
		Assert.Equal(TradeCloseReason.StopOut, trader.TradeLog.Single().CloseReason);
	}

	[Fact]
	public void Advance_OpenPosition_TracksTicksHeldAndDistance()
	{
		var trader = new Trader(1000);
		trader.Apply(Long(1), 99, 100, Now);

		trader.Advance(99, 100, Now.AddSeconds(1));
		trader.Advance(101, 102, Now.AddSeconds(2));

		Assert.Equal(2, trader.CurrentPosition!.Value.TicksHeld);
		Assert.Equal(2, trader.CurrentPosition.Value.CumulativeDistance, 9);
	}

	[Fact]
	public void LimitLong_RestsUntilAskReachesTrigger_ThenFills()
	{
		var trader = new Trader(1000);
		var limit = new SimulationDecision(SimulationAction.EnterLong, 1, OrderType: OrderType.Limit, TriggerPrice: 95);

		var placed = trader.Apply(limit, 99, 100, Now);
		Assert.Null(placed);
		Assert.NotNull(trader.CurrentPendingOrder);
		Assert.Null(trader.CurrentPosition);

		Assert.Empty(trader.Advance(95.5, 96, Now.AddSeconds(1)).AutomaticFills);

		var fill = Assert.Single(trader.Advance(94.9, 95, Now.AddSeconds(2)).AutomaticFills);
		Assert.Equal(SimulationAction.EnterLong, fill.Action);
		Assert.Equal(95, trader.CurrentPosition!.Value.EntryPrice);
		Assert.Null(trader.CurrentPendingOrder);
	}

	[Theory]
	[InlineData(SimulationAction.EnterLong, OrderType.Stop, 105, 104, 105, true)]
	[InlineData(SimulationAction.EnterLong, OrderType.Stop, 105, 103, 104, false)]
	[InlineData(SimulationAction.EnterShort, OrderType.Limit, 105, 105, 106, true)]
	[InlineData(SimulationAction.EnterShort, OrderType.Limit, 105, 104, 105, false)]
	[InlineData(SimulationAction.EnterShort, OrderType.Stop, 95, 95, 96, true)]
	[InlineData(SimulationAction.EnterShort, OrderType.Stop, 95, 96, 97, false)]
	public void PendingOrder_TriggersOnTheCorrectSideOfTheQuote(
		SimulationAction action,
		OrderType orderType,
		double trigger,
		double bid,
		double ask,
		bool expectedFill
	)
	{
		var trader = new Trader(1000);
		trader.Apply(new SimulationDecision(action, 1, OrderType: orderType, TriggerPrice: trigger), 100, 100, Now);

		trader.Advance(bid, ask, Now.AddSeconds(1));

		Assert.Equal(expectedFill, trader.CurrentPosition is not null);
	}

	[Fact]
	public void LimitOrderWithoutTriggerPrice_IsIgnored()
	{
		var trader = new Trader(1000);

		trader.Apply(new SimulationDecision(SimulationAction.EnterLong, 1, OrderType: OrderType.Limit), 100, 100, Now);

		Assert.Null(trader.CurrentPendingOrder);
		Assert.Null(trader.CurrentPosition);
	}

	[Fact]
	public void Close_WhileFlat_CancelsPendingOrder()
	{
		var trader = new Trader(1000);
		trader.Apply(new SimulationDecision(SimulationAction.EnterLong, 1, OrderType: OrderType.Limit, TriggerPrice: 90), 100, 100, Now);

		trader.Apply(CloseDecision, 100, 100, Now);

		Assert.Null(trader.CurrentPendingOrder);
	}

	[Fact]
	public void Snapshot_TracksMaxEquityAndMaxDrawdownHighWaterMarks()
	{
		var trader = new Trader(1000);
		trader.Apply(Long(1), 100, 100, Now);

		var dipped = trader.Snapshot(90, 90);
		trader.Snapshot(110, 110);
		trader.Snapshot(105, 105);

		Assert.Equal(900, dipped.Equity, 9);
		Assert.Equal(1100, trader.MaxEquity, 9);
		Assert.Equal(100, trader.MaxDrawdown, 9);
	}

	[Fact]
	public void Snapshot_WithOpenPosition_ReportsMarginLevel()
	{
		var trader = new Trader(1000, new TraderSettings(Leverage: 10));
		trader.Apply(Long(0.5), 100, 100, Now);

		var state = trader.Snapshot(100, 100);

		Assert.Equal(500, state.MarginUsed, 9);
		Assert.Equal(200, state.MarginLevel!.Value, 9);
		Assert.Equal(500, state.MarginFree, 9);
	}

	[Fact]
	public void ModifyProtection_WithoutPosition_ReturnsFalse()
	{
		Assert.False(new Trader(1000).ModifyProtection(90, 110));
	}

	[Fact]
	public void ModifyProtection_WithPosition_MovesStopAndTargetButKeepsInitialStop()
	{
		var trader = new Trader(1000);
		trader.Apply(Long(1, stopLoss: 90), 100, 100, Now);

		Assert.True(trader.ModifyProtection(95, 120));

		var position = trader.CurrentPosition!.Value;
		Assert.Equal(95, position.StopLossPrice);
		Assert.Equal(120, position.TakeProfitPrice);
		Assert.Equal(90, position.InitialStopLossPrice);
	}

	[Fact]
	public void TradeIds_IncrementPerClosedTrade()
	{
		var trader = new Trader(1000);
		trader.Apply(Long(0.1), 100, 100, Now);
		trader.Apply(CloseDecision, 100, 100, Now);
		trader.Apply(Short(0.1), 100, 100, Now);
		trader.Apply(CloseDecision, 100, 100, Now);

		Assert.Equal(new[] { 1, 2 }, trader.TradeLog.Select(trade => trade.Id));
	}
}
