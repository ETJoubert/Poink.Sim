using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Components.Pages.Arch.Models;

namespace Poink.Sim.Components.Arch;

public partial class ArchDetailDrawer : ComponentBase
{
    [Parameter, EditorRequired] public ArchGraph Graph { get; set; } = default!;

    [Parameter] public ArchComponentNode? Node { get; set; }

    [Parameter] public EventCallback<string> OnSelect { get; set; }

    [Parameter] public EventCallback OnClose { get; set; }

    private sealed record Peer(string Id, string Title);

    private (Peer Peer, string Method)? Producer
    {
        get
        {
            if (Node is null)
                return null;

            var edge = Graph.Edges.FirstOrDefault(e => e.Kind == ArchEdgeKind.Produces && e.ToId == Node.Id);
            if (edge is null)
                return null;

            var peer = Graph.Nodes.FirstOrDefault(n => n.Id == edge.FromId);
            return peer is null ? null : (new Peer(peer.Id, peer.Title), edge.Label);
        }
    }

    private IReadOnlyList<Peer> InjectsInto =>
        Node is null
            ? []
            : Graph.Edges
                .Where(e => e.Kind == ArchEdgeKind.Injected && e.FromId == Node.Id)
                .Select(e => Graph.Nodes.FirstOrDefault(n => n.Id == e.ToId))
                .Where(n => n is not null)
                .Select(n => new Peer(n!.Id, n.Title))
                .Distinct()
                .ToList();

    private Task RaiseSelect(string nodeId) => OnSelect.InvokeAsync(nodeId);

    private Task RaiseSelectByTitle(string title)
    {
        var match = Graph.Nodes.FirstOrDefault(n => n.Title == title);
        return match is null ? Task.CompletedTask : OnSelect.InvokeAsync(match.Id);
    }

    private Task RaiseClose() => OnClose.InvokeAsync();
}
