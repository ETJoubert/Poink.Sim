// ── A resting Limit/Stop entry order, not yet filled ─────────────────────
//  Trader holds at most one of these at a time (mirrors CurrentPosition's
//  one-at-a-time rule). OrderType is always Limit or Stop here -- Market
//  orders never rest, they fill immediately in Trader.Apply.
// ─────────────────────────────────────────────────────────────────────────
using System;
using Poink.Sim.Models;

namespace Poink.Sim.Trader.Orders;

internal readonly record struct PendingOrder(
    PositionDirection Direction, OrderType OrderType, double TriggerPrice, double CapitalFraction,
    double? StopLossPrice, double? TakeProfitPrice, DateTime CreatedTime, string? Reason);
