// ── Recorded RL trajectory shapes ────────────────────────────────────────
//  Produced by RlEpisodeRunner, consumed by an IRlTrainingStrategy. Kept
//  brain/strategy-agnostic: LogProbability is null for any ISimulationBrain
//  that isn't an IPolicyGradientBrain, so RlEpisodeRunner also works as a
//  reward-only evaluator for the existing heuristic brains (no training,
//  just "how did this brain do").
// ─────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models;
using TorchSharp;
using static TorchSharp.torch;

namespace Poink.Sim.Training;

internal sealed record RlStep(SimulationAction Action, Tensor? LogProbability, double Reward);

// LastInputs is null for any ISimulationBrain that isn't an IPolicyGradientBrain
// (nothing to describe), and otherwise a labeled snapshot of what fed the network
// on the episode's final decision -- not every tick, since that's "aggregated"
// enough for the Train page's "what goes in" panel without flooding OnChanged.
//
// Trades/MaxEquity/MaxDrawdown are the episode's own Trader instance's closed-trade
// log and incrementally-tracked equity extremes -- feeds TradeStatistics.From the
// same way SimulationPage's Statistics tab does for a live Trader, so the Train
// page's per-episode/session trader-statistics tables (see RlTrainingService.
// RecentEpisodeTrades) reuse that exact metric set instead of a second one.
internal sealed record RlEpisode(
    IReadOnlyList<RlStep> Steps, CapitalState FinalCapital, IReadOnlyList<RlInputFeature>? LastInputs,
    IReadOnlyList<Trade> Trades, double MaxEquity, double MaxDrawdown);

// One completed episode's trade log/equity extremes, paired with its episode
// number -- retained by RlTrainingService (same bounded window as
// RlEpisodeMetric) so the Train page can derive TradeStatistics per episode,
// or pooled across the whole retained window as a "session" rollup.
internal sealed record RlEpisodeTrades(int EpisodeNumber, IReadOnlyList<Trade> Trades, double MaxEquity, double MaxDrawdown);

// One row of RlTrainingService's rolling metrics log -- the "visual feedback"
// the Train page shows while a run is in progress, so training can actually
// be evaluated against something instead of a single running-average number.
// Public (not internal like everything else here) because it's passed as a
// [Parameter] into EpisodeGrid/RewardChart, and Blazor's generated component
// partial class is always public -- a public parameter can't be typed with a
// less-accessible type.
public sealed record RlEpisodeMetric(int EpisodeNumber, double Reward, double FinalEquity);

// Rolling tally of decisions taken across the current training run -- another
// piece of "what's happening in the background" the Train page's Live tab shows.
internal sealed record RlActionCounts(int Hold, int EnterLong, int EnterShort, int Close)
{
    public static readonly RlActionCounts Empty = new(0, 0, 0, 0);

    public RlActionCounts Add(SimulationAction action) => action switch
    {
        SimulationAction.Hold => this with { Hold = Hold + 1 },
        SimulationAction.EnterLong => this with { EnterLong = EnterLong + 1 },
        SimulationAction.EnterShort => this with { EnterShort = EnterShort + 1 },
        SimulationAction.Close => this with { Close = Close + 1 },
        _ => this,
    };

    public int Total => Hold + EnterLong + EnterShort + Close;
}
