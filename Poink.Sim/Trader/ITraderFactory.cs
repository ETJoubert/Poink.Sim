// ── DI seam for Trader construction ──────────────────────────────────────
//  Lets SimulationEngine/RlEpisodeRunner depend on this instead of `new
//  Trader.Trader(...)` directly, so an orchestration-level unit test can
//  substitute a fake ITraderFactory returning a mock ITrader without a real
//  Trader underneath.
// ─────────────────────────────────────────────────────────────────────────
namespace Poink.Sim.Trader;

internal interface ITraderFactory
{
    ITrader Create(double startingCash, TraderSettings? settings = null);
}
