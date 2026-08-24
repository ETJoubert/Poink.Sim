using System;
using Poink.Sim.Models;

namespace Poink.Sim.Trader.Risk;

internal interface IPositionManager
{
	Position? CurrentPosition { get; }

	RiskContext BuildContext(double bid, double ask, DateTime timestamp);

	TradeExecution? Execute(SimulationDecision decision, double bid, double ask, DateTime timestamp);
}
