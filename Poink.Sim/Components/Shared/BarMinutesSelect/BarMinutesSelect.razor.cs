using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Poink.Sim.Components.Shared.BarMinutesSelect;

// Bar-size dropdown shared by ProfilerPage/SimulationPage/TrainPage -- the
// doubling choice list itself lives in BarMinutesOptions; this just wires it
// up as one reusable @bind-Value control instead of each page repeating the
// same <select>+foreach markup.
public partial class BarMinutesSelect : ComponentBase
{
    [Parameter, EditorRequired] public int Value { get; set; }
    [Parameter] public EventCallback<int> ValueChanged { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public string Label { get; set; } = "Bar minutes";
    [Parameter] public string Title { get; set; } = "Size of each aggregated bar, in minutes";

    private Task OnChanged(ChangeEventArgs e) => ValueChanged.InvokeAsync(int.Parse((string)e.Value!));
}
