using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Training
{
    // The Brain-only half of BrainStrategyPicker's picker row, split out so any
    // page that needs a NN-vs-heuristic brain picker (currently TrainPage via
    // BrainStrategyPicker, and SimulationPage directly) can reuse the same
    // <select>+badge without dragging a StrategyName along.
    public partial class BrainSelect : ComponentBase
    {
        [Parameter, EditorRequired] public IReadOnlyList<BrainOption> BrainOptions { get; set; } = [];

        [Parameter] public string BrainName { get; set; } = "";
        [Parameter] public EventCallback<string> BrainNameChanged { get; set; }

        [Parameter] public string Label { get; set; } = "Brain";
        [Parameter] public bool Disabled { get; set; }

        private IEnumerable<BrainOption> NeuralBrains => BrainOptions.Where(b => b.IsNeuralNetwork);
        private IEnumerable<BrainOption> HeuristicBrains => BrainOptions.Where(b => !b.IsNeuralNetwork);

        private bool SelectedIsNeuralNetwork => BrainOptions.FirstOrDefault(b => b.Name == BrainName)?.IsNeuralNetwork ?? false;

        private Task OnBrainChanged(ChangeEventArgs e) => BrainNameChanged.InvokeAsync(e.Value?.ToString() ?? "");
    }
}
