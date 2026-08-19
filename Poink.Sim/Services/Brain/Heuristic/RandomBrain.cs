
using System;
using Poink.Sim.Models;

namespace Poink.Sim.Brain;

internal sealed class RandomBrain : ISimulationBrain
{
    private readonly double _actionProbability;
    private readonly double _capitalFraction;
    private readonly double _stopLossPercent;
    private readonly double _takeProfitPercent;


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
