// ── Recorded RL trajectory shapes ────────────────────────────────────────
//  Produced by RlEpisodeRunner, consumed by an IRlTrainingStrategy. Kept
//  brain/strategy-agnostic: LogProbability/Entropy are null for any
//  ISimulationBrain that isn't an IPolicyGradientBrain, so RlEpisodeRunner
//  also works as a reward-only evaluator for the existing heuristic brains
//  (no training, just "how did this brain do").
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models;
using TorchSharp;
using static TorchSharp.torch;

namespace Poink.Sim.Training;

// Entropy is the same decision's direction-distribution entropy (see
// IPolicyGradientBrain.DecideWithLogProbability), captured alongside
// LogProbability with no lag -- unlike Reward (which RlEpisodeRunner
// backfills from the *next* decision point, since it measures this action's
// outcome), entropy is intrinsic to what the network chose right now, so it
// needs no such shift.
internal sealed record RlStep(SimulationAction Action, Tensor? LogProbability, double Reward, Tensor? Entropy);

// One decision bar's action plus whatever position was open immediately after
// it (null fields while flat) -- unlike RlStep (trimmed by one entry, training-
// only, see RlEpisodeRunner.Run's doc comment), this is captured unconditionally
// for every brain type and never trimmed, index-aligned 1:1 with BarInputs/
// BarEquity/BarOutputs (same WarmupBars offset from Bars -- see RlEpisode's
// doc comment). Feeds the Train page's episode replay: action markers (see
// TrainLiveTab.DisplayedActionMarkers) and the currently-open position's
// entry/SL/TP lines (see TrainLiveTab.DisplayedPositionLevels), both synced to
// whichever bar is currently revealed rather than only the episode's last one.
//
// Blocked is true when this decision picked a direction/close but Trader.Apply
// silently no-op'd it -- EnterLong/EnterShort while a position was already open
// (Trader.EnterOrQueue's guard), or Close while flat (Trader.Apply's Close
// branch falls through to CancelPendingOrder instead, since there's no open
// position to close). Always false for Hold, which never executes anything to
// begin with. See TrainLiveTab's dud markers/BlockedLongs/BlockedShorts/
// BlockedCloses stats.
internal readonly record struct RlBarAction(
    SimulationAction Action, double? EntryPrice, bool? PositionIsLong, double? StopLossPrice, double? TakeProfitPrice,
    bool Blocked = false);

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
//
// WindowStart/WindowEnd are the first/last tick timestamps this episode actually
// consumed -- the real span sampled from the source data, not the wider fetchSpan
// requested (see RlTrainingService.RandomEpisodeWindow), since the episode stops
// as soon as episodeBars bars complete. Feeds RlTrainingService.RecentEpisodeWindows
// for the Train page's data-coverage timeline.
//
// Bars/BarHistograms are every bar this episode processed, index-aligned 1:1 (see
// BarPriceHistogram.Merge/MergeTime) -- INCLUDING the leading WarmupBars bars (see
// that field below), so the Train page's chart can render the lookback window
// filling up before the first decision, not just the decision bars themselves.
// Empty when aggregate: false (raw-tick mode, unused by RlTrainingService today).
// Feeds RlTrainingService.LatestEpisodeBars/LatestEpisodeBarHistograms for the
// Train page's "current episode" bar chart and volume/price profiles.
//
// WarmupBars is how many of Bars' leading entries were warm-up (history-building,
// no decision -- see RlEpisodeRunner's warmupBars doc comment), so Bars[WarmupBars]
// is the first bar a decision was actually made on. BarInputs/BarEquity/BarOutputs
// below cover only that decision segment, i.e. BarInputs[i] describes Bars[i +
// WarmupBars], not Bars[i] -- callers indexing across both arrays (e.g. TrainLiveTab)
// must apply this offset. 0 for a brain with no windowed encoder.
//
// BarInputs is the same per-bar DescribeInputs snapshot LastInputs takes only once
// (at the episode's final decision), but for every decision bar, index-aligned 1:1
// with Bars[WarmupBars..] (see WarmupBars above) -- so the Train page's episode
// replay can show what the network saw as of whichever decision bar is currently
// revealed, not just the episode's last one. Empty for a non-IPolicyGradientBrain,
// same as LastInputs being null.
//
// BarEquity is index-aligned 1:1 with BarInputs (not Bars -- both share the same
// IPolicyGradientBrain gate and the same WarmupBars offset): each entry is that
// decision's pre-Apply equity (observation.Capital.Equity), one of the two
// anchors (see DenormalizationAnchors) the Train page's de-normalized inputs
// toggle can invert a feature against.
//
// LastOutputs/BarOutputs mirror LastInputs/BarInputs but for what the network
// decided rather than what fed it -- each decision's full RlOutputFeature
// distribution (see that record's doc comment). Unlike LastInputs, LastOutputs
// is NOT independently recomputed from the final observation (see
// RlEpisodeRunner.Run's doc comment on why a second DecideWithLogProbability
// call would risk re-sampling a different action).
internal sealed record RlEpisode(
    IReadOnlyList<RlStep> Steps, CapitalState FinalCapital, IReadOnlyList<RlInputFeature>? LastInputs,
    IReadOnlyList<RlOutputFeature>? LastOutputs, IReadOnlyList<Trade> Trades, double MaxEquity, double MaxDrawdown,
    DateTime WindowStart, DateTime WindowEnd, IReadOnlyList<HlocAvdtBar> Bars, IReadOnlyList<BarPriceHistogram> BarHistograms,
    IReadOnlyList<IReadOnlyList<RlInputFeature>> BarInputs, IReadOnlyList<double> BarEquity,
    IReadOnlyList<IReadOnlyList<RlOutputFeature>> BarOutputs, int WarmupBars, IReadOnlyList<RlBarAction> BarActions);

// One completed episode's trade log/equity extremes, paired with its episode
// number -- retained by RlTrainingService (same bounded window as
// RlEpisodeMetric) so the Train page can derive TradeStatistics per episode,
// or pooled across the whole retained window as a "session" rollup.
//
// BlockedLongs/BlockedShorts/BlockedCloses are this episode's RlBarAction.Blocked
// tally (see that field's doc comment), split by attempted action -- summed
// from the full episode's BarActions once at episode-completion (see
// RlTrainingService.TryStartTraining's loop), not retained as the full
// per-bar list itself (unlike Trades above, which the chart/replay need bar-
// by-bar) since only the count is meaningful once an episode's done.
internal sealed record RlEpisodeTrades(
    int EpisodeNumber, IReadOnlyList<Trade> Trades, double MaxEquity, double MaxDrawdown,
    int BlockedLongs, int BlockedShorts, int BlockedCloses);

// One row of RlTrainingService's rolling metrics log -- the "visual feedback"
// the Train page shows while a run is in progress, so training can actually
// be evaluated against something instead of a single running-average number.
// Public (not internal like everything else here) because it's passed as a
// [Parameter] into EpisodeGrid/RewardChart, and Blazor's generated component
// partial class is always public -- a public parameter can't be typed with a
// less-accessible type.
public sealed record RlEpisodeMetric(int EpisodeNumber, double Reward, double FinalEquity);

// One historic episode's actual sampled window (see RlEpisode's WindowStart/
// WindowEnd doc comment) -- only recorded when training samples from a real
// data range (never for synthetic/dummy episodes, which have no meaningful
// range to plot against). Public for the same reason as RlEpisodeMetric above:
// it's passed as a [Parameter] into EpisodeCoverageTimeline.
public sealed record RlEpisodeWindow(int EpisodeNumber, DateTime Start, DateTime End);

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
