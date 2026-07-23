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

    public RandomBrain(double actionProbability = 0.05, double capitalFraction = 0.5)
    {
        _actionProbability = actionProbability;
        _capitalFraction = capitalFraction;
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
            return new SimulationDecision(action, _capitalFraction, Reason: "random entry");
        }

        return new SimulationDecision(SimulationAction.Close, Reason: "random close");
    }
}
