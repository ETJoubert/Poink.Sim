
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class FlatRequiredForEntryRule : IDirectionMaskRule
{
    public bool IsLegal(SimulationAction action, SimulationObservation observation) =>
        action is not (SimulationAction.EnterLong or SimulationAction.EnterShort) || observation.Position is null;
}
