using Poink.Sim.Models;

namespace Poink.Sim.Brain;

internal interface ISimulationBrain
{
	string Name { get; }

	SimulationDecision Decide(SimulationObservation observation);

	// Called once per run start; stateless brains no-op.
	void Reset();
}
