// ── Composable brain capability: reward computed per decision point ─────
//  Sibling to IRlTrainingStrategy, same "pick by Name" house style (see
//  RlTrainingService's _strategies/StrategyNames) -- this is the piece that
//  used to be a single hardcoded line inside RlEpisodeRunner.RecordStep
//  (reward = equity drift since the previous decision). Pulling it out lets
//  a training run pick which trading rules the reward should encode,
//  without touching PolicyGradientTrainingStrategy's discounting/baseline
//  math at all -- ComputeReward's return value is the same raw per-step
//  reward that always fed that pipeline; only how it's computed changes.
//
//  ComputeReward is a pure function of RewardContext -- no internal state --
//  since IRewardPolicy instances are long-lived DI singletons (see
//  Program.cs) reused across every episode of every run; a stateful
//  implementation would leak state across episode boundaries unless it
//  tracked per-episode resets itself.
// ─────────────────────────────────────────────────────────────────────────
using System;
using Poink.Sim.Models;

namespace Poink.Sim.Training;

// Everything RlEpisodeRunner.RecordStep has on hand at one decision point,
// before that step's own Apply -- PreviousCapital is the running baseline
// (post-Apply snapshot from the previous decision) the previous ComputeReward
// call measured from (see RlEpisodeRunner's doc comment on why reward always
// lands one step "behind" the corresponding Apply); carried as the whole
// CapitalState (not just Equity) so a policy can also read e.g. RealizedPnl's
// own drift, not only the combined Equity figure. PreviousPosition/Position
// are the position open going into the previous/current step respectively
// (null while flat) -- comparing them is how a policy detects "a new position
// was just opened" without needing its own state.
internal readonly record struct RewardContext(
    CapitalState PreviousCapital, CapitalState CapitalNow, Position? PreviousPosition, Position? Position, DateTime TimestampUtc);

internal interface IRewardPolicy
{
    string Name { get; }

    double ComputeReward(RewardContext context);
}
