
using System;
using System.Collections.Generic;
using Poink.Sim.Brain;
using Poink.Sim.Trader.Commission;
using Poink.Sim.Trader.Risk;

namespace Poink.Sim.Trader;

internal sealed record TraderSettings(
    double Leverage = 1.0, double StopOutLevelPercent = 50.0, ICommissionModel? CommissionModel = null,
    IReadOnlyList<string>? RiskPolicyNames = null, RiskPolicyToggles? RiskToggles = null, ISimulationBrain? Brain = null)
{
    public ICommissionModel EffectiveCommissionModel => CommissionModel ?? PercentOfNotionalCommission.None;

    public IReadOnlyList<string> EffectiveRiskPolicyNames => RiskPolicyNames ?? Array.Empty<string>();
}
