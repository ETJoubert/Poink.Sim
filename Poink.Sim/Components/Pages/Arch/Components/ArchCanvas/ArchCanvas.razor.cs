using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Poink.Sim.Components.Pages.Arch.Helpers;
using Poink.Sim.Components.Pages.Arch.Models;

namespace Poink.Sim.Components.Arch;

public partial class ArchCanvas : ComponentBase, IAsyncDisposable
{
	[Inject]
	private IJSRuntime JS { get; set; } = default!;

	[Parameter, EditorRequired]
	public ArchGraph Graph { get; set; } = default!;

	[Parameter]
	public string? SelectedId { get; set; }

	[Parameter]
	public double Zoom { get; set; } = 1.0;

	[Parameter]
	public IReadOnlySet<string>? VisibleAreas { get; set; }

	[Parameter]
	public bool ConnectedOnly { get; set; }

	[Parameter]
	public IReadOnlySet<string>? FocusedNodeIds { get; set; }

	[Parameter]
	public EventCallback<string> OnNodeSelected { get; set; }

	[Parameter]
	public EventCallback OnBackgroundClicked { get; set; }

	[Parameter]
	public EventCallback<double> OnWheelZoom { get; set; }

	private const double CardWidth = ArchNodeLayoutHelper.CardWidth;
	private const double ColumnGap = 90;
	private const double RowGap = 46;
	private const double HeaderHeight = ArchNodeLayoutHelper.HeaderHeight;
	private const double EdgePadding = 30;

	private sealed record NodeLayout(
		ArchComponentNode Node,
		double X,
		double Y,
		double Height,
		IReadOnlyList<ArchPortView> Ports
	);

	private sealed record EdgeLayout(ArchEdge Edge, string Path);

	private IReadOnlyList<NodeLayout> _nodes = [];
	private IReadOnlyList<EdgeLayout> _edges = [];
	private HashSet<string> _connectedNodeIds = [];
	private double _canvasWidth;
	private double _canvasHeight;

	private ElementReference _viewportRef;
	private IJSObjectReference? _panInstance;

	private const string ViewportPanScript = """
		(function() {
		    function bind(viewport) {
		        if (!viewport || viewport.dataset.panBound) return;
		        viewport.dataset.panBound = '1';

		        viewport.addEventListener('pointerdown', function(e) {
		            if (e.button !== 0 || e.target.closest('.anc-card')) return;

		            var startX = e.clientX;
		            var startY = e.clientY;
		            var startScrollLeft = viewport.scrollLeft;
		            var startScrollTop = viewport.scrollTop;
		            var dragged = false;
		            viewport.setPointerCapture(e.pointerId);

		            function onMove(ev) {
		                var dx = ev.clientX - startX;
		                var dy = ev.clientY - startY;
		                if (!dragged && (Math.abs(dx) > 3 || Math.abs(dy) > 3)) {
		                    dragged = true;
		                    viewport.classList.add('acv-panning');
		                }
		                if (dragged) {
		                    viewport.scrollLeft = startScrollLeft - dx;
		                    viewport.scrollTop = startScrollTop - dy;
		                }
		            }
		            function onUp(ev) {
		                viewport.releasePointerCapture(e.pointerId);
		                viewport.removeEventListener('pointermove', onMove);
		                viewport.removeEventListener('pointerup', onUp);
		                viewport.classList.remove('acv-panning');
		                if (dragged) viewport.dataset.suppressClick = '1';
		            }
		            viewport.addEventListener('pointermove', onMove);
		            viewport.addEventListener('pointerup', onUp);
		        });

		        viewport.addEventListener('click', function(e) {
		            if (viewport.dataset.suppressClick === '1') {
		                delete viewport.dataset.suppressClick;
		                e.stopPropagation();
		                e.preventDefault();
		            }
		        });
		    }
		    return { bind: bind };
		})()
		""";

	protected override void OnParametersSet() => BuildLayout();

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (firstRender)
			_panInstance = await JS.InvokeAsync<IJSObjectReference>("eval", ViewportPanScript);

		if (_panInstance is not null)
			await _panInstance.InvokeVoidAsync("bind", _viewportRef);
	}

	public async ValueTask DisposeAsync()
	{
		if (_panInstance is not null)
		{
			try
			{
				await _panInstance.DisposeAsync();
			}
			catch (JSDisconnectedException) { }
		}
	}

	private void BuildLayout()
	{
		if (Graph is null || Graph.Nodes.Count == 0)
		{
			_nodes = [];
			_edges = [];
			_connectedNodeIds = [];
			_canvasWidth = 0;
			_canvasHeight = 0;
			return;
		}

		_connectedNodeIds = [.. Graph.Edges.SelectMany(e => new[] { e.FromId, e.ToId })];

		var visible = Graph.Nodes.Where(IsNodeVisible).ToList();
		if (visible.Count == 0)
		{
			_nodes = [];
			_edges = [];
			_canvasWidth = 0;
			_canvasHeight = 0;
			return;
		}

		// A hidden chip takes no grid slot at all: columns are renumbered to only the distinct
		// Column values still present among visible chips — so filtering compacts the grid
		// instead of leaving holes.
		var columnOrder = visible.Select(n => n.Column).Distinct().OrderBy(c => c).ToList();
		var columnIndex = columnOrder
			.Select((column, index) => (column, index))
			.ToDictionary(p => p.column, p => p.index);

		var heights = visible.ToDictionary(n => n.Id, ArchNodeLayoutHelper.CardHeightFor);

		// Each chip is only as tall as its own port count needs, and chips stack directly under
		// one another from the top of their column — no shared height, no padding to a common row grid.
		var positions = new Dictionary<string, (double X, double Y)>();
		var columnBottom = EdgePadding;
		foreach (var column in visible.GroupBy(n => n.Column))
		{
			var x = EdgePadding + columnIndex[column.Key] * (CardWidth + ColumnGap);
			var y = EdgePadding;
			foreach (var node in column.OrderBy(n => n.Row))
			{
				positions[node.Id] = (x, y);
				y += heights[node.Id] + RowGap;
			}
			columnBottom = Math.Max(columnBottom, y - RowGap);
		}

		_nodes = visible
			.Select(n => new NodeLayout(
				n,
				positions[n.Id].X,
				positions[n.Id].Y,
				heights[n.Id],
				ArchNodeLayoutHelper.BuildPortViews(n)
			))
			.ToList();

		_canvasWidth = EdgePadding * 2 + (columnOrder.Count - 1) * (CardWidth + ColumnGap) + CardWidth;
		_canvasHeight = columnBottom + EdgePadding;

		var drawableEdges = Graph
			.Edges.Where(e => positions.ContainsKey(e.FromId) && positions.ContainsKey(e.ToId))
			.ToList();

		var laid = new List<EdgeLayout>(drawableEdges.Count);
		foreach (var edge in drawableEdges)
		{
			var fromPosition = positions[edge.FromId];
			var toPosition = positions[edge.ToId];

			// A chip's self pin sits at the right of its header, whichever role it plays: the
			// source of an injected dependency or the source of a produced one.
			var start = (X: fromPosition.X + CardWidth, Y: fromPosition.Y + HeaderHeight / 2);

			(double X, double Y) end;
			if (edge.Kind == ArchEdgeKind.Injected)
			{
				var to = FindNode(edge.ToId)!;
				var inputIndex = ArchNodeLayoutHelper.IndexOfPort(to.Inputs, edge.Label);
				if (inputIndex < 0)
					continue;
				end = (toPosition.X, toPosition.Y + ArchNodeLayoutHelper.PortCenterY(inputIndex));
			}
			else
			{
				// Produces: no named port receives it, so the wire lands at the produced
				// chip's top-left corner — the same side every other incoming wire arrives from.
				end = (toPosition.X, toPosition.Y + HeaderHeight / 2);
			}

			laid.Add(BuildEdgeLayout(edge, start, end));
		}

		_edges = laid;
	}

	private ArchComponentNode? FindNode(string nodeId) => Graph.Nodes.FirstOrDefault(n => n.Id == nodeId);

	private static EdgeLayout BuildEdgeLayout(ArchEdge edge, (double X, double Y) start, (double X, double Y) end)
	{
		var dx = Math.Max(46, Math.Abs(end.X - start.X) * 0.5);
		var c1 = (X: start.X + dx, Y: start.Y);
		var c2 = (X: end.X - dx, Y: end.Y);
		var tip = ArchNodeLayoutHelper.PullBack(c2, end);

		var path = string.Create(
			CultureInfo.InvariantCulture,
			$"M {start.X:0.##} {start.Y:0.##} C {c1.X:0.##} {c1.Y:0.##}, {c2.X:0.##} {c2.Y:0.##}, {tip.X:0.##} {tip.Y:0.##}"
		);

		return new EdgeLayout(edge, path);
	}

	private string NodeStyle(NodeLayout layout) =>
		string.Create(
			CultureInfo.InvariantCulture,
			$"left:{layout.X:0.##}px;top:{layout.Y:0.##}px;width:{CardWidth:0.##}px;height:{layout.Height:0.##}px;"
		);

	private bool IsNodeVisible(ArchComponentNode node) =>
		(VisibleAreas is null || VisibleAreas.Contains(node.Area))
		&& (!ConnectedOnly || _connectedNodeIds.Contains(node.Id))
		&& (FocusedNodeIds is not { Count: > 0 } || FocusedNodeIds.Contains(node.Id));

	private static string EdgeClass(EdgeLayout layout) =>
		layout.Edge.Kind == ArchEdgeKind.Produces ? "acv-edge acv-edge-produces" : "acv-edge acv-edge-injected";

	private static string EdgeTooltip(EdgeLayout layout) =>
		layout.Edge.Kind == ArchEdgeKind.Injected
			? $"{layout.Edge.FromTitle} is injected into {layout.Edge.ToTitle} as \"{layout.Edge.Label}\"."
			: $"{layout.Edge.FromTitle}.{layout.Edge.Label}() produces {layout.Edge.ToTitle}.";

	private static string MarkerId(ArchEdgeKind kind) =>
		kind == ArchEdgeKind.Produces ? "acv-arrow-produces" : "acv-arrow-injected";

	private string CanvasStyle =>
		string.Create(
			CultureInfo.InvariantCulture,
			$"width:{_canvasWidth:0.##}px;height:{_canvasHeight:0.##}px;transform:scale({Zoom:0.###});"
		);

	private string SpaceStyle =>
		string.Create(
			CultureInfo.InvariantCulture,
			$"width:{_canvasWidth * Zoom:0.##}px;height:{_canvasHeight * Zoom:0.##}px;"
		);

	private string ViewBox =>
		string.Create(CultureInfo.InvariantCulture, $"0 0 {_canvasWidth:0.##} {_canvasHeight:0.##}");

	private Task RaiseNodeSelected(string nodeId) => OnNodeSelected.InvokeAsync(nodeId);

	private Task RaiseBackgroundClicked() => OnBackgroundClicked.InvokeAsync();

	private Task HandleWheel(WheelEventArgs e) => OnWheelZoom.InvokeAsync(e.DeltaY);
}
