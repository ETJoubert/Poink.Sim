
using System.Linq;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class KeyLevelRequiredForCloseRule : IDirectionMaskRule
{
    private static readonly XGridSignalKind[] KeyLevelSignals =
        [XGridSignalKind.AtPoc, XGridSignalKind.AtVah, XGridSignalKind.AtVal, XGridSignalKind.AtVwap];

    private readonly XGridSignalEncoder _encoder;

    public KeyLevelRequiredForCloseRule(XGridSignalEncoder encoder) => _encoder = encoder;

    public bool IsLegal(SimulationAction action, SimulationObservation observation) =>
        action != SimulationAction.Close || KeyLevelSignals.Any(kind => _encoder.HasSignal(kind, observation));
}
