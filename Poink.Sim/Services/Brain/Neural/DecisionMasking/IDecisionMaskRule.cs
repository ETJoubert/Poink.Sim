using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal interface IDecisionMaskRule
{
	bool IsSatisfied(SimulationObservation observation);
}
