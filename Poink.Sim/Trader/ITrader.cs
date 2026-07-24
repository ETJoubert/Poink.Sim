// ── Trader's public contract ──────────────────────────────────────────────
//  Extracted so callers (SimulationEngine, RlEpisodeRunner) can depend on this
//  instead of the concrete class -- decouples Trader for direct unit testing
//  and lets a test substitute a fake/mock ITrader without touching the real
//  execution engine. Construction still goes through `new Trader.Trader(...)`
//  directly at every call site (see TODO/ for the deferred factory/DI work).
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using Poink.Sim.Models;
using Poink.Sim.Trader.Orders;

namespace Poink.Sim.Trader;

internal interface ITrader
{
    double Cash { get; }
    double RealizedPnl { get; }
    Position? CurrentPosition { get; }
    PendingOrder? CurrentPendingOrder { get; }
    IReadOnlyList<Trade> TradeLog { get; }

    double Leverage { get; }
    double StopOutLevelPercent { get; }
    double MarginUsed { get; }

    double MaxEquity { get; }
    double MaxDrawdown { get; }

    CapitalState Snapshot(double bid, double ask);
    MaintenanceResult Advance(double bid, double ask, DateTime timestamp);
    TradeExecution? Apply(SimulationDecision decision, double bid, double ask, DateTime timestamp);
}
