using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal interface IDirectionMaskRule
{
	bool IsLegal(SimulationAction action, SimulationObservation observation);
}
