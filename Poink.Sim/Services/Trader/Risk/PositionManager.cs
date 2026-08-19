
using System;
using Poink.Sim.Models;

namespace Poink.Sim.Trader.Risk;

internal sealed class PositionManager : IPositionManager
{
    private readonly ITrader _trader;

    public PositionManager(ITrader trader) => _trader = trader;

    public Position? CurrentPosition => _trader.CurrentPosition;

    public RiskContext BuildContext(double bid, double ask, DateTime timestamp) =>
        new(_trader.CurrentPosition, _trader.Snapshot(bid, ask), bid, ask, _trader.Cash, _trader.Leverage, timestamp,
            _trader.TradeLog);

    public TradeExecution? Execute(SimulationDecision decision, double bid, double ask, DateTime timestamp) =>
        _trader.Apply(decision, bid, ask, timestamp);
}
