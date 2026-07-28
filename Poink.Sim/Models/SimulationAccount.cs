// ── Capital / position / trade domain shapes ─────────────────────────────
//  CFD account: leveraged margin trading, spread-aware fills, commission.
//  v1 allows only one open position (and at most one resting pending order)
//  at a time (flat/long/short) -- no pyramiding, no reversing without an
//  explicit Close first.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;

namespace Poink.Sim.Models;

internal enum PositionDirection { Long, Short }

// Why a Trade closed -- Manual covers both a brain-driven Close decision and a
// user's SubmitManualDecision on SimulationPage; StopLoss/TakeProfit/StopOut
// are the three automatic closes Trader.Advance's maintenance pass can fire
// (see its SL/TP/stop-out check order). Distinguishing these lets
// TradeStatistics.From count/sum SL and TP hits separately instead of every
// consumer having to string-match Trade.Reason's free-text message.
internal enum TradeCloseReason { Manual, StopLoss, TakeProfit, StopOut }

// StopLossPrice/TakeProfitPrice are auto-close trigger levels, acted on every
// tick by Trader.Advance (checked at exit-side price -- see UnrealizedPnl).
// TicksHeld/CumulativeDistance are running stats Trader.Advance updates every
// raw tick this position stays open (both start at 0 the tick it opens):
// TicksHeld is a plain count; CumulativeDistance is the sum of |mid-price
// delta| tick-to-tick (path length traveled while holding, not net
// EntryPrice-to-current displacement, so a position that whipsaws back and
// forth accumulates distance even if it ends up near breakeven).
internal readonly record struct Position(
    PositionDirection Direction, double Size, double EntryPrice, DateTime EntryTime,
    double? StopLossPrice = null, double? TakeProfitPrice = null,
    int TicksHeld = 0, double CumulativeDistance = 0)
{
    // Exit-side pricing: a Long exits (and marks) at Bid, a Short at Ask --
    // what the position would actually realize if closed right now, not mid.
    public double UnrealizedPnl(double bid, double ask) =>
        (Direction == PositionDirection.Long ? 1 : -1) * Size *
        ((Direction == PositionDirection.Long ? bid : ask) - EntryPrice);
}

// Read-only snapshot handed to the Brain (and the UI) -- never mutated by the brain.
// MarginUsed: Position.Size * Position.EntryPrice / Leverage, 0 while flat.
// MarginLevel: Equity / MarginUsed * 100, null while flat (nothing to divide by).
internal readonly record struct CapitalState(double Cash, double RealizedPnl, double UnrealizedPnl, double MarginUsed, double? MarginLevel)
{
    public double Equity => Cash + UnrealizedPnl;
    public double MarginFree => Equity - MarginUsed;
}

// One closed round-trip, for the trade log / history. RealizedPnl is net of
// Commission (the round-trip total across both the open and close leg) --
// what TradeStatistics win/loss should read.
internal sealed record Trade(
    int Id, PositionDirection Direction, double Size, double EntryPrice, DateTime EntryTime,
    double ExitPrice, DateTime ExitTime, double RealizedPnl, double Commission, string? Reason,
    TradeCloseReason CloseReason = TradeCloseReason.Manual);

// What Trader.Apply/Advance hands back for a fill (open or close, brain-driven
// or automatic) -- lets the decision log show structured columns instead of a
// single pre-formatted string. Action distinguishes an automatic SL/TP/
// stop-out/pending-trigger fill from a brain-driven one for the log. RealizedPnl
// is null for an open (nothing realized yet); Reason echoes SimulationDecision.Reason
// for brain-driven fills, or a generated trigger description for automatic ones.
internal readonly record struct TradeExecution(
    SimulationAction Action, PositionDirection Direction, double Size, double? RealizedPnl, double Commission, string? Reason);

// Return shape of Trader.Advance -- the per-tick maintenance pass (SL/TP,
// stop-out, pending-order trigger, mark-to-market). AutomaticFills is 0, 1, or
// (rarely, e.g. a stop-out immediately followed by a pending-order trigger in
// the same tick) 2 entries; callers log each one the same way they log a
// brain-driven Apply fill.
internal readonly record struct MaintenanceResult(CapitalState Capital, IReadOnlyList<TradeExecution> AutomaticFills);
