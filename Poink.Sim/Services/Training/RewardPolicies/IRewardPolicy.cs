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
//  tracked per-episode resets itself. OnEpisodeStart is that hook: a no-op
//  default so EquityDriftRewardPolicy/TradingRulesRewardPolicy need no
//  change, but RlEpisodeRunner.Run calls it once at the top of every
//  episode, giving a stateful policy (e.g. DifferentialSharpeRewardPolicy's
//  running mean/variance estimates) a correct place to reset.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
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
// was just opened" without needing its own state. CurrentPrice/RecentBars/
// RecentBarHistograms are the same market-data values RecordStep threads into
// SimulationObservation one line later -- a policy that wants market-structure
// context (e.g. MarketStructureRewardPolicy's Volume/Time Profile reads) needs
// no extra plumbing beyond reading these. RecentBars/RecentBarHistograms are
// empty (Array.Empty<...>(), not just short) whenever the episode runs in
// raw-tick mode (aggregate: false) -- same documented behavior
// SimulationObservation.RecentBarHistograms already has -- so a policy reading
// them must treat empty as "no data this step," not crash.
internal readonly record struct RewardContext(
    CapitalState PreviousCapital, CapitalState CapitalNow, Position? PreviousPosition, Position? Position, DateTime TimestampUtc,
    double CurrentPrice, IReadOnlyList<HlocAvdtBar> RecentBars, IReadOnlyList<BarPriceHistogram> RecentBarHistograms);

internal interface IRewardPolicy
{
    string Name { get; }

    // Called once per episode, before that episode's first ComputeReward --
    // see this file's doc comment for why a stateful policy needs this.
    void OnEpisodeStart() { }

    double ComputeReward(RewardContext context);
}
