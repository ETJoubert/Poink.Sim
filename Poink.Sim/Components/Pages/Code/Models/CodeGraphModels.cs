using System;
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
	Rewards,
}

/// <summary>Left-to-right layout region a card is grouped into on the code diagram.</summary>
public enum CodeRegion
{
	Config,
	Data,
	Inputs,
	Network,
	OutputActions,
	TraderSimulation,
}

public static class CodeConceptExtensions
{
	public static CodeRegion ToRegion(this CodeConcept concept) =>
		concept switch
		{
			CodeConcept.Config => CodeRegion.Config,
			CodeConcept.Data => CodeRegion.Data,
			CodeConcept.Inputs => CodeRegion.Inputs,
			CodeConcept.Network => CodeRegion.Network,
			CodeConcept.Rewards => CodeRegion.Network,
			CodeConcept.Outputs => CodeRegion.OutputActions,
			CodeConcept.Actions => CodeRegion.OutputActions,
			CodeConcept.Trader => CodeRegion.TraderSimulation,
			CodeConcept.Simulation => CodeRegion.TraderSimulation,
			_ => throw new ArgumentOutOfRangeException(nameof(concept), concept, null),
		};
}

public enum CodeEdgeKind
{
	Flow,
	Feedback,
	Drives,
	Config,
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
	string Action,
	IReadOnlyList<CodeTypeRef> Types,
	IReadOnlyList<CodePort> Inputs,
	IReadOnlyList<CodePort> Outputs,
	IReadOnlyList<CodeBadge> Badges,
	IReadOnlyList<CodeSetting> Settings
);

public sealed record CodeEdge(string FromId, string FromPort, string ToId, string ToPort, CodeEdgeKind Kind);

public sealed record CodeGraph(IReadOnlyList<CodeNode> Nodes, IReadOnlyList<CodeEdge> Edges);
