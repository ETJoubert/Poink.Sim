
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class DirectionActionMasker
{
    private readonly IReadOnlyList<IDirectionMaskRule> _rules;

    public DirectionActionMasker(IReadOnlyList<IDirectionMaskRule> rules) => _rules = rules;

    public IReadOnlyList<bool> Mask(IReadOnlyList<SimulationAction> enabledDirections, SimulationObservation observation) =>
        enabledDirections.Select(action => _rules.All(rule => rule.IsLegal(action, observation))).ToList();
}
