
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
