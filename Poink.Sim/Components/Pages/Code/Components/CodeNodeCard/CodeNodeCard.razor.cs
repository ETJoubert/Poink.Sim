using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Poink.Sim.Components.Pages.Code.Models;

namespace Poink.Sim.Components.Code;

public partial class CodeNodeCard : ComponentBase
{
    [Parameter, EditorRequired] public CodeNode Node { get; set; } = default!;

    [Parameter] public IReadOnlyList<CodePortView> Ports { get; set; } = [];

    [Parameter] public IReadOnlyList<CodeSettingView> Settings { get; set; } = [];

    [Parameter] public bool Animated { get; set; }

    [Parameter] public bool IsSelected { get; set; }

    [Parameter] public bool IsDimmed { get; set; }

    [Parameter] public EventCallback<string> OnSelected { get; set; }

    private const int MaxPrimaryTypes = 3;

    private string ConceptClass => $"cnc-{Node.Concept.ToString().ToLowerInvariant()}";

    private string StateClass
    {
        get
        {
            var state = IsSelected ? "cnc-selected" : IsDimmed ? "cnc-dimmed" : string.Empty;
            return Animated ? $"{state} cnc-animated" : state;
        }
    }

    private string PrimaryTypes
    {
        get
        {
            var names = Node.Types.Take(MaxPrimaryTypes).Select(t => t.Name);
            var remaining = Node.Types.Count - MaxPrimaryTypes;
            return remaining > 0 ? $"{string.Join(", ", names)} +{remaining}" : string.Join(", ", names);
        }
    }

    private string PrimaryTypesTooltip => string.Join("\n", Node.Types.Select(t => $"{t.Name} — {t.Path}"));

    private string Tooltip =>
        $"{Node.Title} ({Node.Concept})\n\n{Node.Summary}\n\nClick to trace this object's signal path.";

    private static string PortClass(CodePortView port)
    {
        var side = port.IsOutput ? "cnc-port-out" : "cnc-port-in";
        var wired = port.IsConnected ? string.Empty : " cnc-port-open";
        return $"cnc-port {side}{wired}";
    }

    private static string PortStyle(CodePortView port) => string.Create(
        CultureInfo.InvariantCulture,
        $"top:{port.CenterY:0.##}px;");

    private static string SettingStyle(CodeSettingView setting) => string.Create(
        CultureInfo.InvariantCulture,
        $"top:{setting.CenterY:0.##}px;");

    private static string SettingTooltip(CodeSettingView setting) => setting.IsLive
        ? $"{setting.Name}: {setting.Value}\nRead live from the running services."
        : $"{setting.Name}: {setting.Value}\nDeclared default — the effective value is chosen when a run starts.";

    private Task RaiseSelected() => OnSelected.InvokeAsync(Node.Id);

    private Task HandleKeyDown(KeyboardEventArgs args) =>
        args.Key is "Enter" or " " ? OnSelected.InvokeAsync(Node.Id) : Task.CompletedTask;
}
