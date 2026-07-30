using Microsoft.AspNetCore.Components;
using Poink.Sim.Repository;

namespace Poink.Sim.Components.Training
{
    // Shows "what's actually running" for a live brain -- used by
    // SimulationPage's BRAIN tab. ActiveSnapshot is null for a heuristic
    // brain (nothing to show) or for a neural brain whose live weights don't
    // match any saved snapshot (never saved, or trained further since --
    // see RlTrainingService.GetActiveSnapshot).
    public partial class BrainInfoCard : ComponentBase
    {
        [Parameter, EditorRequired] public string BrainName { get; set; } = "";
        [Parameter] public bool IsNeuralNetwork { get; set; }
        [Parameter] public TrainedModelSummary? ActiveSnapshot { get; set; }
    }
}
