
using System;
using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Trader.Risk;

internal readonly record struct RiskContext(
    Position? Position, CapitalState Capital, double Bid, double Ask,
    double Cash, double Leverage, DateTime Timestamp,
    IReadOnlyList<Trade> Trades);

internal readonly record struct RiskPolicyInfo(string Name, string Description);
