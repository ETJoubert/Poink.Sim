
using Poink.Sim.Models;

namespace Poink.Sim.Trader.Risk;

internal enum RiskVerdict { Permit, Deny, Modify }

internal readonly record struct RiskReview(RiskVerdict Verdict, SimulationDecision Decision, string? Reason, string? PolicyName)
{
    public static RiskReview Permit(SimulationDecision decision) => new(RiskVerdict.Permit, decision, null, null);
    public static RiskReview Deny(SimulationDecision decision, string reason, string policyName) => new(RiskVerdict.Deny, decision, reason, policyName);
    public static RiskReview Modify(SimulationDecision decision, string reason) => new(RiskVerdict.Modify, decision, reason, null);
}
