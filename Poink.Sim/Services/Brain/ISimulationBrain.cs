using Poink.Sim.Models;

namespace Poink.Sim.Brain;

internal interface ISimulationBrain
{
	string Name { get; }

	SimulationDecision Decide(SimulationObservation observation);

	/// <summary>Called once per run start. Stateless brains no-op.</summary>
	void Reset();
}
