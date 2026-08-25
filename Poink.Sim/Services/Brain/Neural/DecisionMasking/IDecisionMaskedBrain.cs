using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Brain.Neural;

internal interface IDecisionMaskedBrain
{
	bool HasActiveDecisionMask { get; }

	bool ShouldDecide(Tick tick, bool barCompletedThisTick, SimulationObservation observation);
}
