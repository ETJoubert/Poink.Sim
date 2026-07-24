// ── CFD account configuration for one Trader ─────────────────────────────
//  Bundled into one settings object (rather than growing Trader's
//  constructor parameter list) so future account-level knobs insert here
//  without touching every call site. Defaults reproduce pre-CFD behavior
//  exactly: Leverage=1 (notional==margin, today's sizing), no commission,
//  a 50% stop-out level that's practically unreachable at 1x leverage.
// ─────────────────────────────────────────────────────────────────────────
using Poink.Sim.Trader.Commission;

namespace Poink.Sim.Trader;

internal sealed record TraderSettings(
    double Leverage = 1.0, double StopOutLevelPercent = 50.0, ICommissionModel? CommissionModel = null)
{
    public ICommissionModel EffectiveCommissionModel => CommissionModel ?? PercentOfNotionalCommission.None;
}
