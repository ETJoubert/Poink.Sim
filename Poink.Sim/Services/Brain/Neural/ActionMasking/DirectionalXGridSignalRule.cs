
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;

namespace Poink.Sim.Brain.Neural;

internal sealed class DirectionalXGridSignalRule : IDirectionMaskRule
{
    private readonly SimulationAction _gatedAction;
    private readonly string _requiredSignal;
    private readonly XGridSignalEncoder _encoder;

    public DirectionalXGridSignalRule(SimulationAction gatedAction, string requiredSignal, XGridSignalEncoder encoder)
    {
        _gatedAction = gatedAction;
        _requiredSignal = requiredSignal;
        _encoder = encoder;
    }

    public bool IsLegal(SimulationAction action, SimulationObservation observation) =>
        action != _gatedAction || _encoder.HasSignal(_requiredSignal, observation);
}
