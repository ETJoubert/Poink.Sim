
using Poink.Sim.Models;

namespace Poink.Sim.Trader.Risk;

internal enum RiskVerdict { Permit, Deny, Modify }

internal readonly record struct RiskReview(RiskVerdict Verdict, SimulationDecision Decision, string? Reason)
{
    public static RiskReview Permit(SimulationDecision decision) => new(RiskVerdict.Permit, decision, null);
    public static RiskReview Deny(SimulationDecision decision, string reason) => new(RiskVerdict.Deny, decision, reason);
    public static RiskReview Modify(SimulationDecision decision, string reason) => new(RiskVerdict.Modify, decision, reason);
}
