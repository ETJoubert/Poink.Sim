
using System;
using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Trader.Risk;

internal readonly record struct RiskContext(
    Position? Position, CapitalState Capital, double Bid, double Ask,
    double Cash, double Leverage, DateTime Timestamp,
    IReadOnlyList<Trade> Trades);

public readonly record struct RiskPolicyInfo(string Name, string Description);

public readonly record struct RiskPolicyParameter(string Key, string Label, double Value, double Min, double Max, double Step);

public sealed record RiskPolicySnapshot(string Name, string Description, bool Enabled, IReadOnlyList<RiskPolicyParameter> Parameters);

public readonly record struct RiskPolicyStatus(string Name, string Status);

public readonly record struct RiskPolicyDenial(string PolicyName, string Reason, DateTime TimestampUtc);


internal enum RiskVerdict { Permit, Deny, Modify }

internal readonly record struct RiskReview(RiskVerdict Verdict, SimulationDecision Decision, string? Reason, string? PolicyName)
{
    public static RiskReview Permit(SimulationDecision decision) => new(RiskVerdict.Permit, decision, null, null);
    public static RiskReview Deny(SimulationDecision decision, string reason, string policyName) => new(RiskVerdict.Deny, decision, reason, policyName);
    public static RiskReview Modify(SimulationDecision decision, string reason) => new(RiskVerdict.Modify, decision, reason, null);
}
