// ── Reward policy: raw equity drift, no shaping ──────────────────────────
//  The baseline -- reward is exactly the trader's equity drift since the
//  previous decision point, nothing added or scaled. RlEpisodeRunner.Run
//  falls back to this whenever a caller doesn't pass a different
//  IRewardPolicy, so existing training behavior/already-trained brains are
//  unaffected by this interface's introduction.
// ─────────────────────────────────────────────────────────────────────────
namespace Poink.Sim.Training;

internal sealed class EquityDriftRewardPolicy : IRewardPolicy
{
    public string Name => "Equity Drift (default)";

    public double ComputeReward(RewardContext context) => context.CapitalNow.Equity - context.PreviousCapital.Equity;
}
