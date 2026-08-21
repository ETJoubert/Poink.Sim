
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

    private const double PriceBucketWidth = 0.01;

    public static RlEpisode Run(
        ISimulationBrain brain, IEnumerable<Tick> ticks, double startingCash,
        int episodeBars, TimeSpan barSize, Trader.ITraderFactory traderFactory,
        bool aggregate = true, Trader.TraderSettings? settings = null, int warmupBars = 0,
        IRewardPolicy? rewardPolicy = null, SimulationSteppingMode steppingMode = SimulationSteppingMode.Bar,
        double cellWidth = 0.10)
    {

        var effectiveRewardPolicy = rewardPolicy ?? new EquityDriftRewardPolicy();

        effectiveRewardPolicy.OnEpisodeStart();

        var trader = traderFactory.Create(startingCash, settings);
        var aggregator = aggregate ? new SimulationBarAggregator(barSize, PriceBucketWidth) : null;
        var cellTracker = aggregate && steppingMode == SimulationSteppingMode.Cell ? new PriceCellTracker(cellWidth) : null;

        var recentBars = new List<HlocAvdtBar>();
        var recentBarHistograms = new List<BarPriceHistogram>();
        var recentTicks = new List<Tick>();
        var steps = new List<RlStep>();


        var allBars = new List<HlocAvdtBar>();
        var allBarHistograms = new List<BarPriceHistogram>();

        var allBarInputs = new List<IReadOnlyList<RlInputFeature>>();
        var allBarEquity = new List<double>();


        var allBarActions = new List<RlBarAction>();


        var allBarOutputs = new List<IReadOnlyList<RlOutputFeature>>();
        var describableBrain = brain as IDescribableBrain;

        double lastBid = 0, lastAsk = 0;

        CapitalState previousCapital = new(startingCash, 0, 0, 0, null);
        Position? previousPosition = null;
        Position? positionAfterDecision = null;
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

            if (!sawTick)
            { windowStart = tick.Timestamp; sawTick = true; }
            windowEnd = tick.Timestamp;

            recentTicks.Add(tick);
            if (recentTicks.Count > MaxRecentTicks)
                recentTicks.RemoveAt(0);


            var maintenance = trader.Advance(tick.Bid, tick.Ask, tick.Timestamp);

            if (aggregator is not null)
            {
                var completed = aggregator.Feed(tick);
                if (completed is not null)
                {
                    recentBars.Add(completed.Value.Bar);
                    if (recentBars.Count > MaxRecentBars)
                        recentBars.RemoveAt(0);
                    recentBarHistograms.Add(completed.Value.Histogram);
                    if (recentBarHistograms.Count > MaxRecentBars)
                        recentBarHistograms.RemoveAt(0);


                    allBars.Add(completed.Value.Bar);
                    allBarHistograms.Add(completed.Value.Histogram);
                }

                // cellTracker.Feed always runs, warmup or not, so its notion of "current
                // cell" doesn't desync from price once decisions are allowed to start.
                bool stillWarmingUp = warmupRemaining > 0;
                if (completed is not null && stillWarmingUp)
                    warmupRemaining--;

                var cellStep = cellTracker?.Feed(tick);
                bool decide = !stillWarmingUp && (cellTracker is not null ? cellStep is not null : completed is not null);

                if (decide)
                {
                    double decisionPrice = completed is { } c ? c.Bar.Close : price;

                    bool policyBlocked;
                    (previousCapital, lastObservation, lastOutputs, positionAfterDecision, policyBlocked) = RecordStep(
                        brain, trader, tick.Timestamp, decisionPrice, tick.Bid, tick.Ask, maintenance.Capital,
                        recentBars, Array.Empty<Tick>(), recentBarHistograms, steps, previousCapital, previousPosition, effectiveRewardPolicy);

                    previousPosition = lastObservation.Value.Position;

                    bool blocked = policyBlocked || steps[^1].Action switch
                    {
                        SimulationAction.EnterLong or SimulationAction.EnterShort => previousPosition is not null,
                        SimulationAction.Close => previousPosition is null,
                        _ => false,
                    };
                    allBarActions.Add(new RlBarAction(
                        steps[^1].Action, positionAfterDecision?.EntryPrice,
                        positionAfterDecision is { } p ? p.Direction == PositionDirection.Long : null,
                        positionAfterDecision?.StopLossPrice, positionAfterDecision?.TakeProfitPrice, tick.Timestamp, blocked));
                    if (describableBrain is not null)
                    {
                        allBarInputs.Add(describableBrain.DescribeInputs(lastObservation.Value));
                        allBarEquity.Add(lastObservation.Value.Capital.Equity);
                        allBarOutputs.Add(lastOutputs!);
                    }
                }

                if (completed is not null && !stillWarmingUp && ++completedBars >= episodeBars)
                    break;
            }
            else
            {

                if (warmupRemaining > 0)
                { warmupRemaining--; continue; }

                (previousCapital, lastObservation, lastOutputs, positionAfterDecision, _) = RecordStep(
                    brain, trader, tick.Timestamp, price, tick.Bid, tick.Ask, maintenance.Capital,
                    Array.Empty<HlocAvdtBar>(), recentTicks, Array.Empty<BarPriceHistogram>(), steps, previousCapital, previousPosition, effectiveRewardPolicy);
                previousPosition = lastObservation.Value.Position;
            }
        }


        if (steps.Count > 0)
            steps.RemoveAt(steps.Count - 1);


        var lastInputs = describableBrain is not null && lastObservation is { } observation
            ? describableBrain.DescribeInputs(observation)
            : null;


        int actualWarmupBars = allBars.Count - completedBars;

        return new RlEpisode(
            steps, trader.Snapshot(lastBid, lastAsk), lastInputs, lastOutputs, trader.TradeLog, trader.MaxEquity, trader.MaxDrawdown,
            windowStart, windowEnd, allBars, allBarHistograms, allBarInputs, allBarEquity, allBarOutputs, actualWarmupBars, allBarActions,
            steppingMode, cellWidth);
    }


    private static (CapitalState Capital, SimulationObservation Observation, IReadOnlyList<RlOutputFeature>? Outputs, Position? PositionAfter, bool PolicyBlocked) RecordStep(
        ISimulationBrain brain, Trader.ITrader trader, DateTime timestamp, double price, double bid, double ask, CapitalState capitalNow,
        IReadOnlyList<HlocAvdtBar> bars, IReadOnlyList<Tick> ticks, IReadOnlyList<BarPriceHistogram> barHistograms,
        List<RlStep> steps, CapitalState previousCapital, Position? previousPosition, IRewardPolicy rewardPolicy)
    {
        var positionNow = trader.CurrentPosition;
        double reward = rewardPolicy.ComputeReward(new RewardContext(
            previousCapital, capitalNow, previousPosition, positionNow, timestamp, price, bars, barHistograms));


        if (steps.Count > 0)
            steps[^1] = steps[^1] with { Reward = reward };

        var observation = new SimulationObservation(
            timestamp, price, bars, ticks, barHistograms, capitalNow, positionNow, trader.TradeLog, bid, ask);

        SimulationDecision decision;
        Tensor? logProbability = null;
        Tensor? entropy = null;
        IReadOnlyList<RlOutputFeature>? outputs = null;
        if (brain is IPolicyGradientBrain policyBrain)
        {
            var (decidedAction, actionLogProbability, decisionEntropy, decisionOutputs) = policyBrain.DecideWithLogProbability(observation);
            decision = decidedAction;
            logProbability = actionLogProbability;
            entropy = decisionEntropy;
            outputs = decisionOutputs;
        }
        else if (brain is INeatPolicyBrain neatBrain)
        {
            var (decidedAction, decisionOutputs) = neatBrain.DecideWithOutputs(observation);
            decision = decidedAction;
            outputs = decisionOutputs;
        }
        else
        {
            decision = brain.Decide(observation);
        }

        var execution = trader.Apply(decision, bid, ask, timestamp);
        var capitalAfter = trader.Snapshot(bid, ask);

        steps.Add(new RlStep(decision.Action, logProbability, 0.0, entropy));
        return (capitalAfter, observation, outputs, trader.CurrentPosition, execution?.Blocked ?? false);
    }
}
