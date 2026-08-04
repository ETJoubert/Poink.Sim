using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Poink.Sim.Components.Shared.BarSizeSelect;


public partial class BarSizeSelect : ComponentBase
{
    [Parameter, EditorRequired] public int Value { get; set; }
    [Parameter] public EventCallback<int> ValueChanged { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public string Label { get; set; } = "Bar size";
    [Parameter] public string Title { get; set; } = "Size of each aggregated bar";

    private Task OnChanged(ChangeEventArgs e) => ValueChanged.InvokeAsync(int.Parse((string)e.Value!));
}
