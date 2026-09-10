using System.Collections.Generic;

namespace Poink.Sim.Components.Pages.TradeMap.Models
{
	/// <summary>The architectural tier a TradeMap node belongs to -- drives both its column and its card styling.</summary>
	public enum TradeMapPartKind
	{
		Page,
		Component,
		Service,
		Helper,
		ExternalApi,
		DataStore,
	}

	/// <summary>
	/// A signal's direction, not its transport: Control is a component/service imperatively calling
	/// something to its left (a method invocation, one-shot request/response); Display is state the
	/// callee exposes continuously -- a property read every render, or a subscribed event.
	/// </summary>
	public enum TradeMapSignalKind
	{
		Control,
		Display,
	}

	/// <summary>A named input or output on a node -- edges attach to these by label, the same contract CodeGraphModels uses for /code.</summary>
	public sealed record TradeMapPort(string Label);

	/// <summary>A port positioned and resolved for rendering -- CenterY is relative to the owning card's top edge.</summary>
	public sealed record TradeMapPortView(string Label, bool IsOutput, double CenterY, bool IsConnected, string Tooltip);

	/// <summary>
	/// One hand-placed box on the map. X/Y/W/H are hand-picked SVG coordinates for visual layout; Column/Row are a
	/// separate, coarser grouping used only to classify how a connector routes (direct curve vs. looped lane) --
	/// they do not drive position the way they do on /code's auto-laid-out grid.
	/// </summary>
	public sealed record TradeMapNode(
		string Id,
		string Title,
		TradeMapPartKind Kind,
		int Column,
		int Row,
		double X,
		double Y,
		double W,
		double H,
		IReadOnlyList<string> Detail,
		IReadOnlyList<TradeMapPort> Inputs,
		IReadOnlyList<TradeMapPort> Outputs
	);

	/// <summary>One hand-authored connector between two named ports. Its on-canvas path is computed, not hand-routed.</summary>
	public sealed record TradeMapEdge(string FromId, string FromPort, string ToId, string ToPort, TradeMapSignalKind Kind);
}
