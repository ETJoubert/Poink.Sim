
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class PositionRequiredForCloseRule : IDirectionMaskRule
{
    public bool IsLegal(SimulationAction action, SimulationObservation observation) =>
        action != SimulationAction.Close || observation.Position is not null;
}
