using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Training
{
    // Own component so the brain/strategy pickers and their NN-vs-heuristic
    // distinction (see BrainOption) can be reused wherever a brain+strategy
    // needs picking, without dragging TrainPage's other run-parameter state
    // along.
    public partial class BrainStrategyPicker : ComponentBase
    {
        [Parameter, EditorRequired] public IReadOnlyList<BrainOption> BrainOptions { get; set; } = [];
        [Parameter, EditorRequired] public IReadOnlyList<string> StrategyNames { get; set; } = [];

        [Parameter] public string BrainName { get; set; } = "";
        [Parameter] public EventCallback<string> BrainNameChanged { get; set; }

        [Parameter] public string StrategyName { get; set; } = "";
        [Parameter] public EventCallback<string> StrategyNameChanged { get; set; }

        [Parameter] public bool Disabled { get; set; }

        private IEnumerable<BrainOption> NeuralBrains => BrainOptions.Where(b => b.IsNeuralNetwork);
        private IEnumerable<BrainOption> HeuristicBrains => BrainOptions.Where(b => !b.IsNeuralNetwork);

        private bool SelectedIsNeuralNetwork => BrainOptions.FirstOrDefault(b => b.Name == BrainName)?.IsNeuralNetwork ?? false;

        private Task OnBrainChanged(ChangeEventArgs e) => BrainNameChanged.InvokeAsync(e.Value?.ToString() ?? "");
        private Task OnStrategyChanged(ChangeEventArgs e) => StrategyNameChanged.InvokeAsync(e.Value?.ToString() ?? "");
    }
}
