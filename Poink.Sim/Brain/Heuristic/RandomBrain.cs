// ── Random placeholder brain ─────────────────────────────────────────────
//  No signal, no state: each decision point independently rolls whether to
//  act at all, then rolls a direction (or closes if already in a position).
//  Exists as a trivial baseline to compare a real strategy against -- and
//  since it's the only brain with any randomness, it's also the one where
//  multiple traders sharing this single instance actually diverge from each
//  other (MomentumThresholdBrain is deterministic given identical inputs).
// ─────────────────────────────────────────────────────────────────────────
using System;
using Poink.Sim.Models;

namespace Poink.Sim.Brain;

internal sealed class RandomBrain : ISimulationBrain
{
    private readonly double _actionProbability;
    private readonly double _capitalFraction;
    private readonly double _stopLossPercent;
    private readonly double _takeProfitPercent;

    // stopLossPercent/takeProfitPercent: distance from entry as a fraction of price
    // (0.003 = 0.3%) -- set on every random entry so Trader.Advance's SL/TP hook
    // (and the chart's SL/TP lines) have something to act on/show. Approximate:
    // computed off CurrentPrice (mid) at decision time, not the actual spread-
    // adjusted fill price Trader.Open uses.
    public RandomBrain(double actionProbability = 0.05, double capitalFraction = 0.5,
        double stopLossPercent = 0.003, double takeProfitPercent = 0.006)
    {
        _actionProbability = actionProbability;
        _capitalFraction = capitalFraction;
        _stopLossPercent = stopLossPercent;
        _takeProfitPercent = takeProfitPercent;
    }

    public string Name => "Random";

    public void Reset() { } // stateless

    public SimulationDecision Decide(SimulationObservation observation)
    {
        if (Random.Shared.NextDouble() > _actionProbability)
            return new SimulationDecision(SimulationAction.Hold);

        if (observation.Position is null)
        {
            var action = Random.Shared.Next(2) == 0 ? SimulationAction.EnterLong : SimulationAction.EnterShort;
            double price = observation.CurrentPrice;
            bool isLong = action == SimulationAction.EnterLong;
            double stopLoss = isLong ? price * (1 - _stopLossPercent) : price * (1 + _stopLossPercent);
            double takeProfit = isLong ? price * (1 + _takeProfitPercent) : price * (1 - _takeProfitPercent);
            return new SimulationDecision(action, _capitalFraction, stopLoss, takeProfit, Reason: "random entry");
        }

        return new SimulationDecision(SimulationAction.Close, Reason: "random close");
    }
}
