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
//  point, marked at the same price both times a position is opened/closed
//  (Trader.Open/Close never jump Cash at the entry/exit instant -- P&L only
//  shows up as the mark price moves against an already-open position). So
//  the reward has to be "equity now vs. equity last time we looked", not
//  "equity before vs. after this one Apply call" (which is always exactly
//  zero) -- previousEquity below carries that running baseline forward.
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
        int episodeBars, TimeSpan barSize, bool aggregate = true)
    {
        var trader = new Trader.Trader(startingCash);
        var aggregator = aggregate ? new SimulationBarAggregator(barSize, PriceBucketWidth) : null;

        var recentBars = new List<HlocAvdtBar>();
        var recentBarHistograms = new List<BarPriceHistogram>();
        var recentTicks = new List<Tick>();
        var steps = new List<RlStep>();

        double lastPrice = 0;
        double previousEquity = startingCash;
        int completedBars = 0;
        SimulationObservation? lastObservation = null;
        foreach (var tick in ticks)
        {
            double price = (tick.Bid + tick.Ask) / 2.0;
            lastPrice = price;

            recentTicks.Add(tick);
            if (recentTicks.Count > MaxRecentTicks) recentTicks.RemoveAt(0);

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
                        brain, trader, tick.Timestamp, completed.Value.Bar.Close, recentBars, Array.Empty<Tick>(),
                        recentBarHistograms, steps, previousEquity);

                    // Episode length is enforced in bars, not by however many ticks the
                    // caller happened to supply -- it only needs to supply "enough"
                    // (see RlTrainingService's fetchSpan sizing).
                    if (++completedBars >= episodeBars) break;
                }
            }
            else
            {
                (previousEquity, lastObservation) = RecordStep(
                    brain, trader, tick.Timestamp, price, Array.Empty<HlocAvdtBar>(), recentTicks,
                    Array.Empty<BarPriceHistogram>(), steps, previousEquity);
            }
        }

        // Aggregated/labeled snapshot of "what goes in" -- the final decision
        // point's own inputs, not a per-tick stream (see RlEpisode's doc comment).
        var lastInputs = brain is IPolicyGradientBrain describableBrain && lastObservation is { } observation
            ? describableBrain.DescribeInputs(observation)
            : null;

        return new RlEpisode(steps, trader.Snapshot(lastPrice), lastInputs, trader.TradeLog, trader.MaxEquity, trader.MaxDrawdown);
    }

    // Returns the post-decision equity (threaded in as the next call's previousEquity)
    // and the observation this step decided from (so the caller can describe the
    // episode's final inputs once the loop ends).
    private static (double Equity, SimulationObservation Observation) RecordStep(
        ISimulationBrain brain, Trader.Trader trader, DateTime timestamp, double price,
        IReadOnlyList<HlocAvdtBar> bars, IReadOnlyList<Tick> ticks, IReadOnlyList<BarPriceHistogram> barHistograms,
        List<RlStep> steps, double previousEquity)
    {
        var capitalNow = trader.Snapshot(price);
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

        trader.Apply(decision, price, timestamp);
        var capitalAfter = trader.Snapshot(price);
        steps.Add(new RlStep(decision.Action, logProbability, reward));
        return (capitalAfter.Equity, observation);
    }
}
