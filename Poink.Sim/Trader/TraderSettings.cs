
using Poink.Sim.Trader.Commission;

namespace Poink.Sim.Trader;

internal sealed record TraderSettings(
    double Leverage = 1.0, double StopOutLevelPercent = 50.0, ICommissionModel? CommissionModel = null)
{
    public ICommissionModel EffectiveCommissionModel => CommissionModel ?? PercentOfNotionalCommission.None;
}
