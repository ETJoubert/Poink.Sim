// ── Headless single-episode playback ─────────────────────────────────────
//  Mirrors SimulationEngine.AdvanceOneUnitLocked/DecideAndApply's tick loop
//  (one Trader, aggregate-or-raw observation building, same recent-bars/
//  recent-ticks windowing) but with no Timer, no locking, no UI publish --
//  training needs to blast through many episodes fast, not play one back at
//  a UI-paced cadence.
//
//  Data-source agnostic: takes a tick sequence rather than generating/
//  fetching ticks itself, so the caller (RlTrainingService) can hand it
//  either a fresh synthetic random-walk list or a lazy, still-streaming
//  Postgres cursor, without this file caring which. Deliberately IEnumerable
//  rather than a materialized list -- the loop below stops as soon as
//  episodeBars bars complete, and a lazy source (see
//  RlTrainingService.RandomEpisodeWindow) means that early stop actually cuts
//  the underlying DB read short instead of having already pulled a much
//  larger window into memory for nothing.
//
//  Reward per decision point = equity drift since the *previous* decision
//  point -- previousEquity carries the running equity baseline forward
//  regardless of source, so a commission charge, a mark-to-market move, or an
//  automatic Trader.Advance fill (SL/TP/stop-out/pending-trigger) between
//  decision points all show up correctly in the next reward, not just what
//  this one Apply call did.
//
//  warmupBars offsets the episode's first decision forward by that many
//  completed bars (ticks, in raw mode) -- those bars still feed recentBars/
//  recentBarHistograms/recentTicks so a windowed encoder's lookback is full
//  real data by the time decisions start, but they produce no RlStep/reward
//  and don't count toward episodeBars. The caller (RlTrainingService) derives
//  this from the brain's own IPolicyGradientBrain.WarmupBars and fetches a
//  correspondingly wider tick span up front. Warm-up bars still land in
//  Bars/BarHistograms (so the Train page's chart can render the lookback
//  window filling up), just not in BarInputs/BarEquity/BarOutputs/Steps --
//  see RlEpisode.WarmupBars for the offset this leaves callers to apply.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using Poink.Sim.Brain;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Simulation;
using TorchSharp;
using static TorchSharp.torch;

namespace Poink.Sim.Training;

internal static class RlEpisodeRunner
{
    private const int MaxRecentBars = 300;
    private const int MaxRecentTicks = 500;

    // Matches SimulationEngine.DefaultPriceBucketWidth -- a brain that reads
    // RecentBarHistograms (e.g. VolumeProfilePolicyBrain) needs training and
    // live Simulation playback to quantize prices the same way.
    private const double PriceBucketWidth = 0.01;

    public static RlEpisode Run(
        ISimulationBrain brain, IEnumerable<Tick> ticks, double startingCash,
        int episodeBars, TimeSpan barSize, Trader.ITraderFactory traderFactory,
        bool aggregate = true, Trader.TraderSettings? settings = null, int warmupBars = 0)
    {
        var trader = traderFactory.Create(startingCash, settings);
        var aggregator = aggregate ? new SimulationBarAggregator(barSize, PriceBucketWidth) : null;

        var recentBars = new List<HlocAvdtBar>();
        var recentBarHistograms = new List<BarPriceHistogram>();
        var recentTicks = new List<Tick>();
        var steps = new List<RlStep>();

        // Every completed bar for the whole episode, uncapped unlike recentBars/
        // recentBarHistograms above (which stay capped to MaxRecentBars purely for
        // what the brain reads each decision) -- naturally bounded by the
        // episodeBars parameter itself, and only ever held for one episode at a
        // time (RlTrainingService overwrites its copy every episode), so there's
        // no unbounded growth risk. Feeds the Train page's "current episode" bar
        // chart/volume/price profiles.
        var allBars = new List<HlocAvdtBar>();
        var allBarHistograms = new List<BarPriceHistogram>();

        // Index-aligned 1:1 with allBars and each other -- only populated for an
        // IPolicyGradientBrain (see RlEpisode's doc comment). allBarEquity is
        // that decision's pre-Apply equity (observation.Capital.Equity, what
        // CapitalStateEncoder actually divided by -- not the post-Apply equity
        // RecordStep returns/threads into previousEquity), so the Train page
        // can invert "unrealized/eq" etc. back to raw currency amounts.
        var allBarInputs = new List<IReadOnlyList<RlInputFeature>>();
        var allBarEquity = new List<double>();

        // Index-aligned 1:1 with allBarInputs -- the same decision's full
        // output distribution (see RlOutputFeature's doc comment), captured
        // straight from RecordStep's own DecideWithLogProbability call rather
        // than a second forward pass, which would risk re-sampling a
        // different action than the one actually taken.
        var allBarOutputs = new List<IReadOnlyList<RlOutputFeature>>();
        var describableBrain = brain as IPolicyGradientBrain;

        double lastBid = 0, lastAsk = 0;
        double previousEquity = startingCash;
        int completedBars = 0;
        int warmupRemaining = warmupBars;
        SimulationObservation? lastObservation = null;
        IReadOnlyList<RlOutputFeature>? lastOutputs = null;
        DateTime windowStart = default, windowEnd = default;
        bool sawTick = false;
        foreach (var tick in ticks)
        {
            double price = (tick.Bid + tick.Ask) / 2.0;
            lastBid = tick.Bid;
            lastAsk = tick.Ask;

            if (!sawTick) { windowStart = tick.Timestamp; sawTick = true; }
            windowEnd = tick.Timestamp;

            recentTicks.Add(tick);
            if (recentTicks.Count > MaxRecentTicks) recentTicks.RemoveAt(0);

            // Unconditional every tick, independent of the aggregation toggle --
            // see SimulationEngine.AdvanceOneUnitLocked for the same reasoning.
            // Automatic fills (SL/TP/stop-out/pending-trigger) need no separate
            // RlStep -- they land in trader.TradeLog for free via Advance's
            // internal Open/Close reuse, and the next decision point's reward
            // naturally absorbs whatever equity drift they caused.
            var maintenance = trader.Advance(tick.Bid, tick.Ask, tick.Timestamp);

            if (aggregator is not null)
            {
                var completed = aggregator.Feed(tick);
                if (completed is not null)
                {
                    recentBars.Add(completed.Value.Bar);
                    if (recentBars.Count > MaxRecentBars) recentBars.RemoveAt(0);
                    recentBarHistograms.Add(completed.Value.Histogram);
                    if (recentBarHistograms.Count > MaxRecentBars) recentBarHistograms.RemoveAt(0);

                    // Every completed bar is recorded for display -- including the
                    // warm-up bars skipped below -- so the Train page's chart can
                    // render the lookback window actually filling up before the
                    // episode's first decision, instead of only ever showing decision
                    // bars (see RlEpisode's WarmupBars doc comment).
                    allBars.Add(completed.Value.Bar);
                    allBarHistograms.Add(completed.Value.Histogram);

                    // Warm-up bars stop here -- no decision, no reward, no
                    // contribution to episodeBars, no BarInputs/BarEquity/BarOutputs
                    // entry (those stay decision-only, offset from Bars by
                    // WarmupBars). This is what lets a windowed encoder's first real
                    // decision see a full lookback of genuine episode data instead of
                    // ever hitting its own zero-padding fallback (see
                    // IObservationEncoder.WarmupBars and RlTrainingService's fetchSpan
                    // sizing, which fetches enough extra ticks up front to cover this).
                    if (warmupRemaining > 0) { warmupRemaining--; continue; }

                    (previousEquity, lastObservation, lastOutputs) = RecordStep(
                        brain, trader, tick.Timestamp, completed.Value.Bar.Close, tick.Bid, tick.Ask, maintenance.Capital,
                        recentBars, Array.Empty<Tick>(), recentBarHistograms, steps, previousEquity);
                    if (describableBrain is not null)
                    {
                        allBarInputs.Add(describableBrain.DescribeInputs(lastObservation.Value));
                        allBarEquity.Add(lastObservation.Value.Capital.Equity);
                        allBarOutputs.Add(lastOutputs!);
                    }

                    // Episode length is enforced in decision bars, not by however many
                    // ticks the caller happened to supply -- it only needs to supply
                    // "enough" (see RlTrainingService's fetchSpan sizing).
                    if (++completedBars >= episodeBars) break;
                }
            }
            else
            {
                // Same warm-up skip as the aggregated branch above, counted in ticks
                // rather than bars since this branch has no bar aggregator.
                if (warmupRemaining > 0) { warmupRemaining--; continue; }

                (previousEquity, lastObservation, lastOutputs) = RecordStep(
                    brain, trader, tick.Timestamp, price, tick.Bid, tick.Ask, maintenance.Capital,
                    Array.Empty<HlocAvdtBar>(), recentTicks, Array.Empty<BarPriceHistogram>(), steps, previousEquity);
            }
        }

        // Aggregated/labeled snapshot of "what goes in" -- the final decision
        // point's own inputs, not a per-tick stream (see RlEpisode's doc comment).
        // Same value as allBarInputs' last entry when aggregating; recomputed
        // here too rather than reused since raw-tick mode (aggregate: false)
        // never populates allBarInputs at all. lastOutputs, unlike lastInputs,
        // is NOT recomputed the same way -- DescribeInputs is a pure re-read,
        // but a second DecideWithLogProbability call would re-sample and could
        // pick a different action than the one actually taken, so it's instead
        // carried forward from RecordStep's own return each iteration.
        var lastInputs = describableBrain is not null && lastObservation is { } observation
            ? describableBrain.DescribeInputs(observation)
            : null;

        // Actual, not requested -- allBars.Count is the true count of bars processed
        // (warm-up + decision) and completedBars is the true count of decision bars,
        // so this stays correct even if the tick stream ran out mid-warm-up (e.g. a
        // misconfigured fetchSpan), rather than trusting the warmupBars parameter as-is.
        int actualWarmupBars = allBars.Count - completedBars;

        return new RlEpisode(
            steps, trader.Snapshot(lastBid, lastAsk), lastInputs, lastOutputs, trader.TradeLog, trader.MaxEquity, trader.MaxDrawdown,
            windowStart, windowEnd, allBars, allBarHistograms, allBarInputs, allBarEquity, allBarOutputs, actualWarmupBars);
    }

    // Returns the post-decision equity (threaded in as the next call's previousEquity),
    // the observation this step decided from (so the caller can describe the episode's
    // final inputs once the loop ends), and -- only for an IPolicyGradientBrain -- that
    // decision's full output distribution (null otherwise). capitalNow is this tick's
    // already-computed Advance result, so Trader.Snapshot isn't called twice.
    private static (double Equity, SimulationObservation Observation, IReadOnlyList<RlOutputFeature>? Outputs) RecordStep(
        ISimulationBrain brain, Trader.ITrader trader, DateTime timestamp, double price, double bid, double ask, CapitalState capitalNow,
        IReadOnlyList<HlocAvdtBar> bars, IReadOnlyList<Tick> ticks, IReadOnlyList<BarPriceHistogram> barHistograms,
        List<RlStep> steps, double previousEquity)
    {
        double reward = capitalNow.Equity - previousEquity;
        var observation = new SimulationObservation(
            timestamp, price, bars, ticks, barHistograms, capitalNow, trader.CurrentPosition);

        SimulationDecision decision;
        Tensor? logProbability = null;
        IReadOnlyList<RlOutputFeature>? outputs = null;
        if (brain is IPolicyGradientBrain policyBrain)
        {
            var (decidedAction, actionLogProbability, decisionOutputs) = policyBrain.DecideWithLogProbability(observation);
            decision = decidedAction;
            logProbability = actionLogProbability;
            outputs = decisionOutputs;
        }
        else
        {
            decision = brain.Decide(observation);
        }

        trader.Apply(decision, bid, ask, timestamp);
        var capitalAfter = trader.Snapshot(bid, ask);
        steps.Add(new RlStep(decision.Action, logProbability, reward));
        return (capitalAfter.Equity, observation, outputs);
    }
}
