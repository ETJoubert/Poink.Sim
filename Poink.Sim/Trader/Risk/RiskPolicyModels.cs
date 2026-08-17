
using System;
using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Trader.Risk;

internal readonly record struct RiskContext(
    Position? Position, CapitalState Capital, double Bid, double Ask,
    double Cash, double Leverage, DateTime Timestamp,
    IReadOnlyList<Trade> Trades);

public readonly record struct RiskPolicyInfo(string Name, string Description);

public sealed record RiskPolicySnapshot(string Name, string Description, bool Enabled);

public readonly record struct RiskPolicyStatus(string Name, string Status);

public readonly record struct RiskPolicyDenial(string PolicyName, string Reason, DateTime TimestampUtc);
