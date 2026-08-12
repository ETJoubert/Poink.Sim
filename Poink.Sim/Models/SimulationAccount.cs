
using System;
using System.Collections.Generic;

namespace Poink.Sim.Models;

internal enum PositionDirection { Long, Short }


internal enum TradeCloseReason { Manual, StopLoss, TakeProfit, StopOut }

internal readonly record struct Position(
    PositionDirection Direction, double Size, double EntryPrice, DateTime EntryTime,
    double? StopLossPrice = null, double? TakeProfitPrice = null,
    int TicksHeld = 0, double CumulativeDistance = 0)
{

    public double UnrealizedPnl(double bid, double ask) =>
        (Direction == PositionDirection.Long ? 1 : -1) * Size *
        ((Direction == PositionDirection.Long ? bid : ask) - EntryPrice);
}


internal readonly record struct CapitalState(double Cash, double RealizedPnl, double UnrealizedPnl, double MarginUsed, double? MarginLevel)
{
    public double Equity => Cash + UnrealizedPnl;
    public double MarginFree => Equity - MarginUsed;
}


internal sealed record Trade(
    int Id, PositionDirection Direction, double Size, double EntryPrice, DateTime EntryTime,
    double ExitPrice, DateTime ExitTime, double RealizedPnl, double Commission, string? Reason,
    TradeCloseReason CloseReason = TradeCloseReason.Manual);


internal readonly record struct TradeExecution(
    SimulationAction Action, PositionDirection Direction, double Size, double? RealizedPnl, double Commission, string? Reason,
    bool Blocked = false);


internal readonly record struct MaintenanceResult(CapitalState Capital, IReadOnlyList<TradeExecution> AutomaticFills);
