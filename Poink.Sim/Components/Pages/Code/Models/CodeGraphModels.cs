using System.Collections.Generic;

namespace Poink.Sim.Components.Pages.Code.Models;

public enum CodeConcept
{
    Config,
    Data,
    Inputs,
    Network,
    Outputs,
    Actions,
    Trader,
    Simulation,
    Rewards
}

public enum CodeEdgeKind
{
    Flow,
    Feedback,
    Drives,
    Config
}

public sealed record CodePort(string Label);

public sealed record CodePortView(string Label, bool IsOutput, double CenterY, bool IsConnected, string Tooltip);

public sealed record CodeSetting(string Name, string Value, bool IsLive = false);

public sealed record CodeSettingView(string Name, string Value, bool IsLive, double CenterY);

public sealed record CodeTypeRef(string Name, string Path);

public sealed record CodeBadge(string Label, string Value);

public sealed record CodeNode(
    string Id,
    string Title,
    CodeConcept Concept,
    int Column,
    int Row,
    string Summary,
    IReadOnlyList<CodeTypeRef> Types,
    IReadOnlyList<CodePort> Inputs,
    IReadOnlyList<CodePort> Outputs,
    IReadOnlyList<CodeBadge> Badges,
    IReadOnlyList<CodeSetting> Settings);

public sealed record CodeEdge(string FromId, string FromPort, string ToId, string ToPort, CodeEdgeKind Kind);

public sealed record CodeGraph(IReadOnlyList<CodeNode> Nodes, IReadOnlyList<CodeEdge> Edges);
