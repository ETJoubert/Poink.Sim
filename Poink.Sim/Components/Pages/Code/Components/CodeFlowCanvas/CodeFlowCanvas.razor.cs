using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Poink.Sim.Components.Pages.Code.Models;

namespace Poink.Sim.Components.Code;

public partial class CodeFlowCanvas : ComponentBase, IAsyncDisposable
{
	[Inject]
	private IJSRuntime JS { get; set; } = default!;

	[Parameter, EditorRequired]
	public CodeGraph Graph { get; set; } = default!;

	[Parameter]
	public string? SelectedId { get; set; }

	[Parameter]
	public IReadOnlySet<string>? HighlightedNodeIds { get; set; }

	[Parameter]
	public IReadOnlySet<CodeConcept>? VisibleConcepts { get; set; }

	[Parameter]
	public double Zoom { get; set; } = 1.0;

	[Parameter]
	public bool Animated { get; set; }

	[Parameter]
	public EventCallback<string> OnNodeSelected { get; set; }

	[Parameter]
	public EventCallback OnBackgroundClicked { get; set; }

	[Parameter]
	public EventCallback<double> OnWheelZoom { get; set; }

	private const double CardWidth = 250;
	private const double ColumnGap = 92;
	private const double RowGap = 52;
	private const double HeaderHeight = 22;
	private const double PortsPad = 8;
	private const double PortRowHeight = 16;
	private const double FooterHeight = 32;
	private const double EdgePadding = 26;
	private const double LaneGap = 42;
	private const double LaneStub = 26;
	private const double CornerRadius = 9;
	private const int LongSpanThreshold = 3;
	private const double PortClearance = 7;

	private enum EdgeRoute
	{
		Bezier,
		LocalLane,
		GlobalLane,
	}

	private sealed record NodeLayout(
		CodeNode Node,
		double X,
		double Y,
		IReadOnlyList<CodePortView> Ports,
		IReadOnlyList<CodeSettingView> Settings
	);

	private sealed record EdgeLayout(CodeEdge Edge, CodeConcept SourceConcept, string Path);

	private IReadOnlyList<NodeLayout> _nodes = [];
	private IReadOnlyList<EdgeLayout> _edges = [];
	private double _cardHeight;
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
		            if (e.button !== 0 || e.target.closest('.cnc-card')) return;

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
		                    viewport.classList.add('cfc-panning');
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
		                viewport.classList.remove('cfc-panning');
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
			return;
		}

		var maxBodyRows = Graph.Nodes.Max(n => Math.Max(Math.Max(n.Inputs.Count, n.Outputs.Count), n.Settings.Count));
		_cardHeight = HeaderHeight + PortsPad * 2 + maxBodyRows * PortRowHeight + FooterHeight;

		var positions = Graph.Nodes.ToDictionary(
			n => n.Id,
			n => (X: EdgePadding + n.Column * (CardWidth + ColumnGap), Y: EdgePadding + n.Row * (_cardHeight + RowGap))
		);

		_nodes = Graph
			.Nodes.Select(n => new NodeLayout(
				n,
				positions[n.Id].X,
				positions[n.Id].Y,
				BuildPortViews(n),
				BuildSettingViews(n)
			))
			.ToList();

		var contentBottom = _nodes.Max(n => n.Y) + _cardHeight;
		var routes = Graph.Edges.ToDictionary(e => e, ClassifyRoute);
		var globalLanes = AssignLanes(routes);

		_canvasWidth = EdgePadding * 2 + Graph.Nodes.Max(n => n.Column) * (CardWidth + ColumnGap) + CardWidth;
		_canvasHeight = contentBottom + globalLanes.Count * LaneGap + EdgePadding;

		var laid = new List<EdgeLayout>(Graph.Edges.Count);
		foreach (var edge in Graph.Edges)
		{
			var from = FindNode(edge.FromId);
			var to = FindNode(edge.ToId);
			if (from is null || to is null)
				continue;

			var outputIndex = IndexOfPort(from.Outputs, edge.FromPort);
			var inputIndex = IndexOfPort(to.Inputs, edge.ToPort);
			if (outputIndex < 0 || inputIndex < 0)
				continue;

			var fromPosition = positions[from.Id];
			var toPosition = positions[to.Id];
			var start = (X: fromPosition.X + CardWidth, Y: fromPosition.Y + PortCenterY(outputIndex));
			var end = (X: toPosition.X, Y: toPosition.Y + PortCenterY(inputIndex));

			var route = routes[edge];
			var laneY = route switch
			{
				EdgeRoute.GlobalLane => contentBottom + LaneGap * (globalLanes[edge] + 1),
				EdgeRoute.LocalLane => LocalLaneY(fromPosition.Y, toPosition.Y),
				_ => 0,
			};

			laid.Add(BuildEdgeLayout(edge, from.Concept, start, end, route, laneY));
		}

		_edges = laid;
	}

	private static double PortCenterY(int index) => HeaderHeight + PortsPad + index * PortRowHeight + PortRowHeight / 2;

	private double LocalLaneY(double fromY, double toY) =>
		toY > fromY ? (fromY + _cardHeight + toY) / 2 : (toY + _cardHeight + fromY) / 2;

	private static int IndexOfPort(IReadOnlyList<CodePort> ports, string label)
	{
		for (var index = 0; index < ports.Count; index++)
		{
			if (ports[index].Label == label)
				return index;
		}
		return -1;
	}

	private IReadOnlyList<CodePortView> BuildPortViews(CodeNode node)
	{
		var views = new List<CodePortView>(node.Inputs.Count + node.Outputs.Count);

		for (var index = 0; index < node.Inputs.Count; index++)
		{
			var label = node.Inputs[index].Label;
			var sources = Graph
				.Edges.Where(e => e.ToId == node.Id && e.ToPort == label)
				.Select(e => FindNode(e.FromId)?.Title)
				.Where(title => title is not null)
				.Distinct()
				.ToList();

			views.Add(
				new CodePortView(
					label,
					IsOutput: false,
					PortCenterY(index),
					sources.Count > 0,
					sources.Count > 0
						? $"INPUT — {label}\nfrom {string.Join(", ", sources)}"
						: $"INPUT — {label}\nsupplied from outside the diagram"
				)
			);
		}

		for (var index = 0; index < node.Outputs.Count; index++)
		{
			var label = node.Outputs[index].Label;
			var targets = Graph
				.Edges.Where(e => e.FromId == node.Id && e.FromPort == label)
				.Select(e => FindNode(e.ToId)?.Title)
				.Where(title => title is not null)
				.Distinct()
				.ToList();

			views.Add(
				new CodePortView(
					label,
					IsOutput: true,
					PortCenterY(index),
					targets.Count > 0,
					targets.Count > 0
						? $"OUTPUT — {label}\nto {string.Join(", ", targets)}"
						: $"OUTPUT — {label}\nconsumed outside the diagram"
				)
			);
		}

		return views;
	}

	private static IReadOnlyList<CodeSettingView> BuildSettingViews(CodeNode node) =>
		node
			.Settings.Select(
				(setting, index) => new CodeSettingView(setting.Name, setting.Value, setting.IsLive, PortCenterY(index))
			)
			.ToList();

	private CodeNode? FindNode(string nodeId) => Graph.Nodes.FirstOrDefault(n => n.Id == nodeId);

	private EdgeRoute ClassifyRoute(CodeEdge edge)
	{
		var from = FindNode(edge.FromId);
		var to = FindNode(edge.ToId);
		if (from is null || to is null)
			return EdgeRoute.Bezier;

		if (edge.Kind == CodeEdgeKind.Feedback)
			return EdgeRoute.GlobalLane;

		var span = to.Column - from.Column;
		if (span > 0)
			return span > LongSpanThreshold ? EdgeRoute.GlobalLane : EdgeRoute.Bezier;

		return span >= -1 && Math.Abs(to.Row - from.Row) == 1 ? EdgeRoute.LocalLane : EdgeRoute.GlobalLane;
	}

	private Dictionary<CodeEdge, int> AssignLanes(IReadOnlyDictionary<CodeEdge, EdgeRoute> routes)
	{
		var lanes = new Dictionary<CodeEdge, int>();
		var ordered = routes
			.Where(pair => pair.Value == EdgeRoute.GlobalLane)
			.Select(pair => pair.Key)
			.OrderBy(ColumnSpan)
			.ToList();

		for (var index = 0; index < ordered.Count; index++)
			lanes[ordered[index]] = index;

		return lanes;
	}

	private int ColumnSpan(CodeEdge edge)
	{
		var from = FindNode(edge.FromId);
		var to = FindNode(edge.ToId);
		return from is null || to is null ? 0 : Math.Abs(to.Column - from.Column);
	}

	private static EdgeLayout BuildEdgeLayout(
		CodeEdge edge,
		CodeConcept sourceConcept,
		(double X, double Y) start,
		(double X, double Y) end,
		EdgeRoute route,
		double laneY
	)
	{
		if (route == EdgeRoute.Bezier)
		{
			var dx = Math.Max(46, (end.X - start.X) * 0.55);
			var c1 = (X: start.X + dx, Y: start.Y);
			var c2 = (X: end.X - dx, Y: end.Y);
			var tip = PullBack(c2, end);

			var curve = string.Create(
				CultureInfo.InvariantCulture,
				$"M {start.X:0.##} {start.Y:0.##} C {c1.X:0.##} {c1.Y:0.##}, {c2.X:0.##} {c2.Y:0.##}, {tip.X:0.##} {tip.Y:0.##}"
			);

			return new EdgeLayout(edge, sourceConcept, curve);
		}

		var outX = start.X + LaneStub;
		var inX = end.X - LaneStub;
		var routed = BuildOrthogonalPath([
			start,
			(outX, start.Y),
			(outX, laneY),
			(inX, laneY),
			(inX, end.Y),
			PullBack((inX, end.Y), end),
		]);

		return new EdgeLayout(edge, sourceConcept, routed);
	}

	private static (double X, double Y) PullBack((double X, double Y) from, (double X, double Y) target)
	{
		var dx = target.X - from.X;
		var dy = target.Y - from.Y;
		var length = Math.Sqrt(dx * dx + dy * dy);
		return length < PortClearance
			? target
			: (target.X - dx / length * PortClearance, target.Y - dy / length * PortClearance);
	}

	private static string BuildOrthogonalPath(IReadOnlyList<(double X, double Y)> points)
	{
		var builder = new StringBuilder();
		builder.Append(CultureInfo.InvariantCulture, $"M {points[0].X:0.##} {points[0].Y:0.##}");

		for (var index = 1; index < points.Count - 1; index++)
		{
			var previous = points[index - 1];
			var corner = points[index];
			var next = points[index + 1];

			var radius = Math.Min(CornerRadius, Math.Min(Distance(previous, corner), Distance(corner, next)) / 2);

			if (radius <= 0.5)
			{
				builder.Append(CultureInfo.InvariantCulture, $" L {corner.X:0.##} {corner.Y:0.##}");
				continue;
			}

			var entry = Towards(corner, previous, radius);
			var exit = Towards(corner, next, radius);
			builder.Append(CultureInfo.InvariantCulture, $" L {entry.X:0.##} {entry.Y:0.##}");
			builder.Append(
				CultureInfo.InvariantCulture,
				$" Q {corner.X:0.##} {corner.Y:0.##}, {exit.X:0.##} {exit.Y:0.##}"
			);
		}

		var last = points[^1];
		builder.Append(CultureInfo.InvariantCulture, $" L {last.X:0.##} {last.Y:0.##}");
		return builder.ToString();
	}

	private static double Distance((double X, double Y) a, (double X, double Y) b) =>
		Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));

	private static (double X, double Y) Towards((double X, double Y) from, (double X, double Y) to, double distance)
	{
		var length = Distance(from, to);
		return length <= 0
			? from
			: (from.X + (to.X - from.X) / length * distance, from.Y + (to.Y - from.Y) / length * distance);
	}

	private bool IsDimmed(string nodeId) =>
		HighlightedNodeIds is { Count: > 0 } && !HighlightedNodeIds.Contains(nodeId);

	private bool IsVisible(CodeConcept concept) =>
		VisibleConcepts is null || VisibleConcepts.Count == 0 || VisibleConcepts.Contains(concept);

	private string NodeWrapperClass(NodeLayout layout) =>
		IsVisible(layout.Node.Concept) ? "cfc-node" : "cfc-node cfc-node-hidden";

	private string NodeStyle(NodeLayout layout) =>
		string.Create(
			CultureInfo.InvariantCulture,
			$"left:{layout.X:0.##}px;top:{layout.Y:0.##}px;width:{CardWidth:0.##}px;height:{_cardHeight:0.##}px;"
		);

	private string EdgeClass(EdgeLayout layout)
	{
		var kind = layout.Edge.Kind.ToString().ToLowerInvariant();
		var concept = layout.SourceConcept.ToString().ToLowerInvariant();
		var dimmed = IsEdgeDimmed(layout) ? " cfc-dimmed" : string.Empty;
		var hidden = IsEdgeVisible(layout) ? string.Empty : " cfc-edge-hidden";
		return $"cfc-edge cfc-edge-{kind} cfc-source-{concept}{dimmed}{hidden}";
	}

	private bool IsEdgeDimmed(EdgeLayout layout) =>
		HighlightedNodeIds is { Count: > 0 } highlighted
		&& !(highlighted.Contains(layout.Edge.FromId) && highlighted.Contains(layout.Edge.ToId));

	private bool IsEdgeVisible(EdgeLayout layout)
	{
		var from = FindNode(layout.Edge.FromId);
		var to = FindNode(layout.Edge.ToId);
		return from is not null && to is not null && IsVisible(from.Concept) && IsVisible(to.Concept);
	}

	private static string EdgeTooltip(EdgeLayout layout) =>
		$"{layout.Edge.FromId} · {layout.Edge.FromPort}  ->  {layout.Edge.ToId} · {layout.Edge.ToPort}";

	private static string MarkerId(CodeEdgeKind kind) =>
		kind switch
		{
			CodeEdgeKind.Feedback => "cfc-arrow-feedback",
			CodeEdgeKind.Drives => "cfc-arrow-drives",
			CodeEdgeKind.Config => "cfc-arrow-config",
			_ => "cfc-arrow-flow",
		};

	private string CanvasClass => Animated ? "cfc-canvas cfc-animated" : "cfc-canvas";

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
