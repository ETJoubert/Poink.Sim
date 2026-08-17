using System.Collections.Generic;

namespace Poink.Sim.Components.Pages.Arch.Models;

public enum ArchEdgeKind
{
    Injected,
    Produces
}

public sealed record ArchTypeRef(string Name, string Path);

public sealed record ArchPort(string Name, string TypeName, string Tooltip, bool IsEvent = false, string? WiredFrom = null);

public sealed record ArchPortView(string Name, bool IsOutput, double CenterY, bool IsConnected, string Tooltip);

public sealed record ArchMethod(string Signature, string Tooltip, string? Produces = null);

public sealed record ArchComponentNode(
    string Id,
    string Title,
    string Area,
    int Column,
    int Row,
    IReadOnlyList<ArchTypeRef> Types,
    string Summary,
    ArchPort SelfPort,
    IReadOnlyList<ArchPort> Inputs,
    IReadOnlyList<ArchMethod> Methods,
    IReadOnlyList<ArchPort> Outputs);

public sealed record ArchEdge(string FromId, string FromTitle, string ToId, string ToTitle, string Label, ArchEdgeKind Kind);

public sealed record ArchGraph(IReadOnlyList<ArchComponentNode> Nodes, IReadOnlyList<ArchEdge> Edges);
