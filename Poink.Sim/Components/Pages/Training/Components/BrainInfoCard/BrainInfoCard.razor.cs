using Microsoft.AspNetCore.Components;
using Poink.Sim.Repository;

namespace Poink.Sim.Components.Training
{

    public partial class BrainInfoCard : ComponentBase
    {
        [Parameter, EditorRequired] public string BrainName { get; set; } = "";
        [Parameter] public bool IsNeuralNetwork { get; set; }
        [Parameter] public TrainedModelSummary? ActiveSnapshot { get; set; }
    }
}
