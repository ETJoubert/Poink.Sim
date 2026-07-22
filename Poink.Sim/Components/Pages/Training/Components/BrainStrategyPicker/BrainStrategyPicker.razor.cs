using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Training
{
    // Own component so the brain/strategy pickers and their NN-vs-heuristic
    // distinction (see BrainOption) can be reused wherever a brain+strategy
    // needs picking, without dragging TrainPage's other run-parameter state
    // along. The brain half itself is BrainSelect, reused as-is by
    // SimulationPage where only a brain (no strategy) needs picking.
    public partial class BrainStrategyPicker : ComponentBase
    {
        [Parameter, EditorRequired] public IReadOnlyList<BrainOption> BrainOptions { get; set; } = [];
        [Parameter, EditorRequired] public IReadOnlyList<string> StrategyNames { get; set; } = [];

        [Parameter] public string BrainName { get; set; } = "";
        [Parameter] public EventCallback<string> BrainNameChanged { get; set; }

        [Parameter] public string StrategyName { get; set; } = "";
        [Parameter] public EventCallback<string> StrategyNameChanged { get; set; }

        [Parameter] public bool Disabled { get; set; }

        private Task OnStrategyChanged(ChangeEventArgs e) => StrategyNameChanged.InvokeAsync(e.Value?.ToString() ?? "");
    }
}
