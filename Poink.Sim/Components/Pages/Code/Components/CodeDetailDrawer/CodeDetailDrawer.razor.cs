using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Components.Pages.Code.Models;

namespace Poink.Sim.Components.Code;

public partial class CodeDetailDrawer : ComponentBase
{
    [Parameter, EditorRequired] public CodeGraph Graph { get; set; } = default!;

    [Parameter] public CodeNode? Node { get; set; }

    [Parameter] public EventCallback<string> OnSelect { get; set; }

    [Parameter] public EventCallback OnClose { get; set; }

    private sealed record Peer(string Id, string Title);

    private sealed record Wire(string Port, IReadOnlyList<Peer> Peers);

    private string ConceptClass => Node is null ? string.Empty : $"cdd-{Node.Concept.ToString().ToLowerInvariant()}";

    private IReadOnlyList<Wire> Incoming =>
        Node is null
            ? []
            : Node.Inputs
                .Select(port => new Wire(
                    port.Label,
                    ResolvePeers(Graph.Edges.Where(e => e.ToId == Node.Id && e.ToPort == port.Label).Select(e => e.FromId))))
                .ToList();

    private IReadOnlyList<Wire> Outgoing =>
        Node is null
            ? []
            : Node.Outputs
                .Select(port => new Wire(
                    port.Label,
                    ResolvePeers(Graph.Edges.Where(e => e.FromId == Node.Id && e.FromPort == port.Label).Select(e => e.ToId))))
                .ToList();

    private IReadOnlyList<Peer> ResolvePeers(IEnumerable<string> nodeIds) =>
        nodeIds
            .Distinct()
            .Select(id => Graph.Nodes.FirstOrDefault(n => n.Id == id))
            .Where(n => n is not null)
            .Select(n => new Peer(n!.Id, n.Title))
            .ToList();

    private Task RaiseSelect(string nodeId) => OnSelect.InvokeAsync(nodeId);

    private Task RaiseClose() => OnClose.InvokeAsync();
}
