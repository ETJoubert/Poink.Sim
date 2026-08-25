using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Poink.Sim.Components.Pages.Arch.Models;
using Poink.Sim.Components.Pages.Arch.Services;

namespace Poink.Sim.Components.Pages;

public partial class ArchPage : ComponentBase
{
	[Inject]
	private IArchGraphProvider GraphProvider { get; set; } = default!;

	[Inject]
	private ILogger<ArchPage> Logger { get; set; } = default!;

	private const double MinZoom = 0.5;
	private const double MaxZoom = 1.5;
	private const double ZoomStep = 0.05;
	private const double DefaultZoom = 1.0;

	private ArchGraph _graph = new([], []);
	private string? _selectedId;
	private HashSet<string> _highlighted = [];
	private double _zoom = DefaultZoom;
	private HashSet<string> _visibleAreas = [];
	private bool _connectedOnly;

	protected override void OnInitialized()
	{
		_graph = GraphProvider.Build();
		_visibleAreas = [.. AllAreas];

		Logger.LogInformation(
			"Arch page opened: {ComponentCount} chips across {AreaCount} areas, {InputCount} input ports, "
				+ "{MethodCount} methods, {OutputCount} output ports, {WireCount} wires resolved by reflection",
			_graph.Nodes.Count,
			_visibleAreas.Count,
			InputPortCount,
			MethodCount,
			OutputPortCount,
			_graph.Edges.Count
		);
	}

	private IReadOnlyList<string> AllAreas =>
		[.. _graph.Nodes.Select(n => n.Area).Distinct().OrderBy(a => a, StringComparer.Ordinal)];

	private int InputPortCount => _graph.Nodes.Sum(n => n.Inputs.Count);

	private int MethodCount => _graph.Nodes.Sum(n => n.Methods.Count);

	private int OutputPortCount => _graph.Nodes.Sum(n => n.Outputs.Count);

	private int WireCount => _graph.Edges.Count;

	private int WiredChipCount
	{
		get
		{
			var wired = new HashSet<string>();
			foreach (var edge in _graph.Edges)
			{
				wired.Add(edge.FromId);
				wired.Add(edge.ToId);
			}
			return wired.Count;
		}
	}

	private ArchComponentNode? SelectedNode =>
		_selectedId is null ? null : _graph.Nodes.FirstOrDefault(n => n.Id == _selectedId);

	private void SelectNode(string nodeId)
	{
		if (_selectedId == nodeId)
		{
			ClearSelection();
			return;
		}

		_selectedId = nodeId;
		_highlighted = BuildDependencyClosure(nodeId);
		Logger.LogInformation(
			"Arch page traced {NodeId}: {HighlightedCount} chips in its dependency closure",
			nodeId,
			_highlighted.Count
		);
	}

	private void ClearSelection()
	{
		_selectedId = null;
		_highlighted = [];
	}

	// Walks both directions of the wiring so a click shows the chip's full blast radius,
	// not just its immediate neighbours.
	private HashSet<string> BuildDependencyClosure(string nodeId)
	{
		var traced = new HashSet<string> { nodeId };

		Walk(nodeId, id => _graph.Edges.Where(e => e.ToId == id).Select(e => e.FromId));
		Walk(nodeId, id => _graph.Edges.Where(e => e.FromId == id).Select(e => e.ToId));

		return traced;

		void Walk(string start, Func<string, IEnumerable<string>> next)
		{
			var pending = new Queue<string>();
			pending.Enqueue(start);

			while (pending.Count > 0)
			{
				var current = pending.Dequeue();
				foreach (var neighbour in next(current))
				{
					if (traced.Add(neighbour))
						pending.Enqueue(neighbour);
				}
			}
		}
	}

	private bool IsAreaVisible(string area) => _visibleAreas.Contains(area);

	private void ToggleArea(string area)
	{
		if (!_visibleAreas.Remove(area))
			_visibleAreas.Add(area);
	}

	private void ToggleAllAreas()
	{
		_visibleAreas = _visibleAreas.Count == 0 ? [.. AllAreas] : [];
	}

	private string AllAreasTooltip =>
		_visibleAreas.Count == 0 ? "Every area is hidden. Click to show them all again." : "Hide every area.";

	private void ToggleConnectedOnly()
	{
		_connectedOnly = !_connectedOnly;
		Logger.LogInformation("Arch page connected-only filter {State}", _connectedOnly ? "enabled" : "disabled");
	}

	private string AreaTooltip(string area)
	{
		var count = _graph.Nodes.Count(n => n.Area == area);
		return $"{area}: {count} chip{(count == 1 ? "" : "s")}. Click to show or hide.";
	}

	private string ConnectedOnlyTooltip =>
		_connectedOnly
			? "Showing only chips with at least one wire. Click to show every chip again."
			: $"{WiredChipCount} of {_graph.Nodes.Count} chips have at least one wire. Click to hide the rest.";

	private void ZoomIn() => _zoom = Math.Min(MaxZoom, Math.Round(_zoom + ZoomStep, 2));

	private void ZoomOut() => _zoom = Math.Max(MinZoom, Math.Round(_zoom - ZoomStep, 2));

	private void ResetView()
	{
		_zoom = DefaultZoom;
		_visibleAreas = [.. AllAreas];
		_connectedOnly = false;
		ClearSelection();
	}

	private void HandleWheelZoom(double deltaY)
	{
		if (deltaY < 0)
			ZoomIn();
		else if (deltaY > 0)
			ZoomOut();
	}

	private string ZoomLabel => (_zoom * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
}
