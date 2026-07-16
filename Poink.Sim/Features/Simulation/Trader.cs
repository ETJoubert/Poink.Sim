// ── Trade execution + P&L ────────────────────────────────────────────────
//  Applies Brain decisions to Capital/Position. Simple cash P&L, no
//  leverage/margin: capital moves 1:1 with price × size. Trades execute at
//  mid price (spread/commission not modeled -- Tick.Bid/Ask are already
//  available for wiring that in later). v1 requires an explicit Close
//  before reversing direction; only one open position at a time.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;

namespace Poink.Sim.Features.Simulation;

internal sealed class Trader
{
    private readonly List<Trade> _trades = new();
    private int _nextTradeId = 1;

    public Trader(double startingCash) => Cash = startingCash;

    public double Cash { get; private set; }
    public double RealizedPnl { get; private set; }
    public Position? CurrentPosition { get; private set; }
    public IReadOnlyList<Trade> TradeLog => _trades;

    public CapitalState Snapshot(double currentPrice) =>
        new(Cash, RealizedPnl, CurrentPosition?.UnrealizedPnl(currentPrice) ?? 0);

    // Applies one Brain decision at the given mark price/time. Returns a short
    // description of what happened (for the log), or null for a no-op.
    public string? Apply(SimulationDecision decision, double currentPrice, DateTime timestamp)
    {
        // SL/TP CHECK (not implemented yet): if CurrentPosition is set and its
        // StopLossPrice/TakeProfitPrice is breached by currentPrice, force-close
        // here before considering the Brain's decision, tagging Reason accordingly.

        return decision.Action switch
        {
            SimulationAction.Hold => null,
            SimulationAction.Close => Close(currentPrice, timestamp, decision.Reason),
            SimulationAction.EnterLong => Open(PositionDirection.Long, decision, currentPrice, timestamp),
            SimulationAction.EnterShort => Open(PositionDirection.Short, decision, currentPrice, timestamp),
            _ => null,
        };
    }

    private string? Open(PositionDirection direction, SimulationDecision decision, double price, DateTime time)
    {
        if (CurrentPosition is not null || price <= 0) return null;

        double fraction = Math.Clamp(decision.CapitalFraction, 0, 1);
        double committed = fraction * Cash;
        if (committed <= 0) return null;

        double size = committed / price;
        CurrentPosition = new Position(direction, size, price, time, decision.StopLossPrice, decision.TakeProfitPrice);
        return $"{(direction == PositionDirection.Long ? "BUY" : "SHORT")} {size:F4} @ {price:F5}" +
            (decision.Reason is null ? "" : $" ({decision.Reason})");
    }

    private string? Close(double price, DateTime time, string? reason)
    {
        if (CurrentPosition is not { } position) return null;

        double pnl = position.UnrealizedPnl(price);
        Cash += pnl;
        RealizedPnl += pnl;
        _trades.Add(new Trade(_nextTradeId++, position.Direction, position.Size, position.EntryPrice,
            position.EntryTime, price, time, pnl, reason));
        CurrentPosition = null;
        return $"CLOSE {position.Direction} {position.Size:F4} @ {price:F5} pnl={pnl:F2}" +
            (reason is null ? "" : $" ({reason})");
    }
}
