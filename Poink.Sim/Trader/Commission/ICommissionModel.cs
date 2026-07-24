// ── Pluggable per-fill commission cost ───────────────────────────────────
//  Trader charges this once per fill leg (open and close are separate calls),
//  so a round-trip trade's total commission is CommissionFor(open) +
//  CommissionFor(close). Kept as an interface (not a fixed rate on Trader
//  itself) so alternate schedules -- flat fee, per-lot -- can be swapped in
//  without touching Trader; PercentOfNotionalCommission is the only
//  implementation today.
// ─────────────────────────────────────────────────────────────────────────
namespace Poink.Sim.Trader.Commission;

internal interface ICommissionModel
{
    double CommissionFor(double size, double fillPrice);
}
