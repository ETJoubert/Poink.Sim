// ── Capital / position / trade domain shapes ─────────────────────────────
//  Simple cash P&L, no leverage or margin: capital moves 1:1 with
//  price × position size, long or short. v1 allows only one open position
//  at a time (flat/long/short) -- no pyramiding, no reversing without an
//  explicit Close first.
// ─────────────────────────────────────────────────────────────────────────
using System;

namespace Poink.Sim.Models;

internal enum PositionDirection { Long, Short }

// StopLossPrice/TakeProfitPrice are the stop-loss/take-profit extension point:
// present and settable today (from a SimulationDecision), but Trader.Apply does
// not yet act on them -- see the "SL/TP CHECK" marker there. Wiring auto-close
// in later is "read these two fields during mark-to-market," not a rework.
internal readonly record struct Position(
    PositionDirection Direction, double Size, double EntryPrice, DateTime EntryTime,
    double? StopLossPrice = null, double? TakeProfitPrice = null)
{
    public double UnrealizedPnl(double currentPrice) =>
        (Direction == PositionDirection.Long ? 1 : -1) * Size * (currentPrice - EntryPrice);
}

// Read-only snapshot handed to the Brain (and the UI) -- never mutated by the brain.
internal readonly record struct CapitalState(double Cash, double RealizedPnl, double UnrealizedPnl)
{
    public double Equity => Cash + UnrealizedPnl;
}

// One closed round-trip, for the trade log / history.
internal sealed record Trade(
    int Id, PositionDirection Direction, double Size, double EntryPrice, DateTime EntryTime,
    double ExitPrice, DateTime ExitTime, double RealizedPnl, string? Reason);
