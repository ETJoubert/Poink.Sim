using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Poink.Sim.Components.Pages.Arch.Models;

namespace Poink.Sim.Components.Arch;

public partial class ArchNodeCard : ComponentBase
{
    [Parameter, EditorRequired] public ArchComponentNode Node { get; set; } = default!;

    [Parameter] public IReadOnlyList<ArchPortView> Ports { get; set; } = [];

    [Parameter] public bool IsSelected { get; set; }

    [Parameter] public EventCallback<string> OnSelected { get; set; }

    private string StateClass => IsSelected ? "anc-selected" : string.Empty;

    private string AreaColorStyle => $"--anc-color:{ArchAreaPalette.ColorFor(Node.Area)};";

    private string TypesLabel => string.Join(" : ", Node.Types.Select(t => t.Name));

    private string TypesTooltip => string.Join("\n", Node.Types.Select(t => $"{t.Name} — {t.Path}"));

    private string Tooltip => $"{Node.Title}\n\n{Node.Summary}\n\nClick for the full port and method list.";

    private string SelfPortClass => Node.SelfPort.WiredFrom is null ? "anc-self anc-self-open" : "anc-self anc-self-wired";

    private static string PortClass(ArchPortView port)
    {
        var side = port.IsOutput ? "anc-port-out" : "anc-port-in";
        var wired = port.IsConnected ? string.Empty : " anc-port-open";
        return $"anc-port {side}{wired}";
    }

    private static string PortStyle(ArchPortView port) => string.Create(CultureInfo.InvariantCulture, $"top:{port.CenterY:0.##}px;");

    private Task RaiseSelected() => OnSelected.InvokeAsync(Node.Id);

    private Task HandleKeyDown(KeyboardEventArgs args) =>
        args.Key is "Enter" or " " ? OnSelected.InvokeAsync(Node.Id) : Task.CompletedTask;
}
