using System;
using Poink.Sim.Models;

namespace Poink.Sim.Trader.Orders;

internal readonly record struct PendingOrder(
	PositionDirection Direction,
	OrderType OrderType,
	double TriggerPrice,
	double CapitalFraction,
	double? StopLossPrice,
	double? TakeProfitPrice,
	DateTime CreatedTime,
	string? Reason
);
