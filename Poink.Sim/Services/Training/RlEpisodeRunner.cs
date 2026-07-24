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
        int episodeBars, TimeSpan barSize, bool aggregate = true, Trader.TraderSettings? settings = null)
    {
        var trader = new Trader.Trader(startingCash, settings);
        var aggregator = aggregate ? new SimulationBarAggregator(barSize, PriceBucketWidth) : null;

        var recentBars = new List<HlocAvdtBar>();
        var recentBarHistograms = new List<BarPriceHistogram>();
        var recentTicks = new List<Tick>();
        var steps = new List<RlStep>();

        double lastBid = 0, lastAsk = 0;
        double previousEquity = startingCash;
        int completedBars = 0;
        SimulationObservation? lastObservation = null;
        foreach (var tick in ticks)
        {
            double price = (tick.Bid + tick.Ask) / 2.0;
            lastBid = tick.Bid;
            lastAsk = tick.Ask;

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
                    (previousEquity, lastObservation) = RecordStep(
                        brain, trader, tick.Timestamp, completed.Value.Bar.Close, tick.Bid, tick.Ask, maintenance.Capital,
                        recentBars, Array.Empty<Tick>(), recentBarHistograms, steps, previousEquity);

                    // Episode length is enforced in bars, not by however many ticks the
                    // caller happened to supply -- it only needs to supply "enough"
                    // (see RlTrainingService's fetchSpan sizing).
                    if (++completedBars >= episodeBars) break;
                }
            }
            else
            {
                (previousEquity, lastObservation) = RecordStep(
                    brain, trader, tick.Timestamp, price, tick.Bid, tick.Ask, maintenance.Capital,
                    Array.Empty<HlocAvdtBar>(), recentTicks, Array.Empty<BarPriceHistogram>(), steps, previousEquity);
            }
        }

        // Aggregated/labeled snapshot of "what goes in" -- the final decision
        // point's own inputs, not a per-tick stream (see RlEpisode's doc comment).
        var lastInputs = brain is IPolicyGradientBrain describableBrain && lastObservation is { } observation
            ? describableBrain.DescribeInputs(observation)
            : null;

        return new RlEpisode(steps, trader.Snapshot(lastBid, lastAsk), lastInputs, trader.TradeLog, trader.MaxEquity, trader.MaxDrawdown);
    }

    // Returns the post-decision equity (threaded in as the next call's previousEquity)
    // and the observation this step decided from (so the caller can describe the
    // episode's final inputs once the loop ends). capitalNow is this tick's
    // already-computed Advance result, so Trader.Snapshot isn't called twice.
    private static (double Equity, SimulationObservation Observation) RecordStep(
        ISimulationBrain brain, Trader.ITrader trader, DateTime timestamp, double price, double bid, double ask, CapitalState capitalNow,
        IReadOnlyList<HlocAvdtBar> bars, IReadOnlyList<Tick> ticks, IReadOnlyList<BarPriceHistogram> barHistograms,
        List<RlStep> steps, double previousEquity)
    {
        double reward = capitalNow.Equity - previousEquity;
        var observation = new SimulationObservation(
            timestamp, price, bars, ticks, barHistograms, capitalNow, trader.CurrentPosition);

        SimulationDecision decision;
        Tensor? logProbability = null;
        if (brain is IPolicyGradientBrain policyBrain)
        {
            var (decidedAction, actionLogProbability) = policyBrain.DecideWithLogProbability(observation);
            decision = decidedAction;
            logProbability = actionLogProbability;
        }
        else
        {
            decision = brain.Decide(observation);
        }

        trader.Apply(decision, bid, ask, timestamp);
        var capitalAfter = trader.Snapshot(bid, ask);
        steps.Add(new RlStep(decision.Action, logProbability, reward));
        return (capitalAfter.Equity, observation);
    }
}
