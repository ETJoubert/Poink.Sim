using System;
using System.Collections.Generic;
using Poink.Sim.Models;
using Poink.Sim.Trader.Risk;

namespace Poink.Sim.Tests.TestSupport;

internal static class RiskContextHelper
{
	public static RiskContext Flat(double cash = 1000, double leverage = 10, double bid = 100, double ask = 100) =>
		new(null, new CapitalState(cash, 0, 0, 0, null), bid, ask, cash, leverage, TestTickHelper.Origin, Array.Empty<Trade>());

	public static RiskContext WithPosition(Position position, double bid, double ask, double cash = 1000, double leverage = 1) =>
		new(
			position,
			new CapitalState(cash, 0, position.UnrealizedPnl(bid, ask), 0, null),
			bid,
			ask,
			cash,
			leverage,
			TestTickHelper.Origin,
			Array.Empty<Trade>()
		);

	public static RiskContext WithTrades(IReadOnlyList<Trade> trades) => Flat() with { Trades = trades };

	public static Trade ClosedTrade(int id, double realizedPnl) =>
		new(id, PositionDirection.Long, 1, 100, TestTickHelper.Origin, 100 + realizedPnl, TestTickHelper.Origin, realizedPnl, 0, null);
}
