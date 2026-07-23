// ── Heuristic placeholder brain ──────────────────────────────────────────
//  Rule-based (no training, no weights): compares the current price to the
//  price Lookback observations ago. A move beyond +/-ThresholdPercent opens
//  (or holds) a position in that direction; a move that fades back toward
//  flat closes an open position. This exists to prove the MarketSim/Trader/
//  Brain loop end-to-end -- swap in a trained ISimulationBrain later without
//  touching the interface (see ISimulationBrain.cs).
// ─────────────────────────────────────────────────────────────────────────
using System;
using Poink.Sim.Models;

namespace Poink.Sim.Brain;

internal sealed class MomentumThresholdBrain : ISimulationBrain
{
    private readonly int _lookback;
    private readonly double _thresholdPercent;
    private readonly double _revertPercent;
    private readonly double _capitalFraction;

    public MomentumThresholdBrain(
        int lookback = 10, double thresholdPercent = 0.15,
        double revertPercent = 0.05, double capitalFraction = 0.5)
    {
        _lookback = lookback;
        _thresholdPercent = thresholdPercent;
        _revertPercent = revertPercent;
        _capitalFraction = capitalFraction;
    }

    public string Name => "Momentum Threshold";

    public void Reset() { } // stateless

    public SimulationDecision Decide(SimulationObservation observation)
    {
        double? basePrice = observation.RecentBars.Count > 0
            ? LookbackPrice(observation.RecentBars.Count, i => observation.RecentBars[i].Close)
            : LookbackPrice(observation.RecentTicks.Count, i => (observation.RecentTicks[i].Bid + observation.RecentTicks[i].Ask) / 2.0);

        if (basePrice is null || basePrice == 0)
            return new SimulationDecision(SimulationAction.Hold);

        double momentumPercent = (observation.CurrentPrice - basePrice.Value) / basePrice.Value * 100.0;
        var position = observation.Position;

        if (momentumPercent > _thresholdPercent)
        {
            if (position is null)
                return new SimulationDecision(SimulationAction.EnterLong, _capitalFraction,
                    Reason: $"momentum +{momentumPercent:F3}% over {_lookback}");
            if (position.Value.Direction == PositionDirection.Short)
                return new SimulationDecision(SimulationAction.Close, Reason: "momentum flipped up");
            return new SimulationDecision(SimulationAction.Hold);
        }

        if (momentumPercent < -_thresholdPercent)
        {
            if (position is null)
                return new SimulationDecision(SimulationAction.EnterShort, _capitalFraction,
                    Reason: $"momentum {momentumPercent:F3}% over {_lookback}");
            if (position.Value.Direction == PositionDirection.Long)
                return new SimulationDecision(SimulationAction.Close, Reason: "momentum flipped down");
            return new SimulationDecision(SimulationAction.Hold);
        }

        if (position is not null && Math.Abs(momentumPercent) < _revertPercent)
            return new SimulationDecision(SimulationAction.Close, Reason: "momentum reverted toward flat");

        return new SimulationDecision(SimulationAction.Hold);
    }

    // Reads a single price at the lookback point directly off the source buffer
    // instead of copying the whole (up to 5000-tick) window just to index into
    // it -- priceAt is only ever invoked once.
    private double? LookbackPrice(int count, Func<int, double> priceAt)
    {
        if (count < 2) return null;
        int index = Math.Max(0, count - 1 - _lookback);
        return priceAt(index);
    }
}
