using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Poink.Sim.Components.Pages.TradeMap.Models;

namespace Poink.Sim.Components.TradeMap
{
	/// <summary>
	/// Routes connectors between the hand-placed node cards the same way /code's CodeFlowCanvas routes its
	/// auto-laid-out ones, with one addition: a port's side depends on its signal kind, not just its direction.
	/// A Display port keeps input-left/output-right (state flowing forward to be displayed); a Control port
	/// flips to input-right/output-left (an action flowing backward to whatever originated it). A card is
	/// split top/bottom to match -- Display ports stack down from the header, Control ports stack up from the
	/// footer -- so the two directions never share a lane. A route whose ports face each other (forward Display,
	/// backward Control) curves directly; one whose ports face away (same column, or a signal running against
	/// its own kind's grain) loops through a routed lane instead.
	/// </summary>
	public partial class TradeMapCanvas : ComponentBase, IAsyncDisposable
	{
		[Inject]
		private IJSRuntime JS { get; set; } = default!;

		[Parameter, EditorRequired]
		public IReadOnlyList<TradeMapNode> Nodes { get; set; } = [];

		[Parameter, EditorRequired]
		public IReadOnlyList<TradeMapEdge> Edges { get; set; } = [];

		[Parameter]
		public string? SelectedId { get; set; }

		[Parameter]
		public IReadOnlySet<string>? HighlightedNodeIds { get; set; }

		[Parameter]
		public IReadOnlySet<TradeMapPartKind>? VisibleKinds { get; set; }

		[Parameter]
		public double Zoom { get; set; } = 1.0;

		[Parameter]
		public EventCallback<string> OnNodeSelected { get; set; }

		[Parameter]
		public EventCallback OnBackgroundClicked { get; set; }

		[Parameter]
		public EventCallback<double> OnWheelZoom { get; set; }

		private const double HeaderHeight = 40;
		private const double PortsPad = 8;
		private const double PortRowHeight = 16;
		private const double EdgePadding = 40;
		private const double LaneGap = 44;
		private const double LaneStub = 28;
		private const double CornerRadius = 9;
		private const int LongSpanThreshold = 3;
		private const double PortClearance = 7;

		private enum EdgeRoute
		{
			Bezier,
			LocalLane,
			GlobalLane,
		}

		private readonly record struct PortKey(string NodeId, string Label, bool IsOutput);

		private sealed record PortAnchor(double X, double Y, TradeMapSignalKind Kind);

		private sealed record EdgeLayout(TradeMapEdge Edge, string Path);

		private double _canvasWidth;
		private double _canvasHeight;
		private IReadOnlyList<EdgeLayout> _edgeLayouts = [];
		private IReadOnlyDictionary<PortKey, TradeMapPortView> _portViews = new Dictionary<PortKey, TradeMapPortView>();

		private ElementReference _viewportRef;
		private IJSObjectReference? _panInstance;

		// Drag-to-pan on empty canvas space, ignoring drags started on a node card.
		private const string ViewportPanScript = """
			(function() {
			    function bind(viewport) {
			        if (!viewport || viewport.dataset.panBound) return;
			        viewport.dataset.panBound = '1';

			        viewport.addEventListener('pointerdown', function(e) {
			            if (e.button !== 0 || e.target.closest('.tmnc-card')) return;

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
			                    viewport.classList.add('tmc-panning');
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
			                viewport.classList.remove('tmc-panning');
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
			if (Nodes.Count == 0)
			{
				_canvasWidth = 0;
				_canvasHeight = 0;
				_edgeLayouts = [];
				_portViews = new Dictionary<PortKey, TradeMapPortView>();
				return;
			}

			_canvasWidth = EdgePadding * 2 + Nodes.Max(n => n.X + n.W);
			var contentBottom = Nodes.Max(n => n.Y + n.H);

			var anchors = BuildPortAnchors();
			var routes = Edges.ToDictionary(e => e, ClassifyRoute);
			var globalLanes = AssignLanes(routes);

			_canvasHeight = contentBottom + EdgePadding + (globalLanes.Count > 0 ? globalLanes.Count * LaneGap : 0);

			var laid = new List<EdgeLayout>(Edges.Count);
			foreach (var edge in Edges)
			{
				if (
					!anchors.TryGetValue(new PortKey(edge.FromId, edge.FromPort, true), out var start)
					|| !anchors.TryGetValue(new PortKey(edge.ToId, edge.ToPort, false), out var end)
				)
					continue;

				var from = FindNode(edge.FromId);
				var to = FindNode(edge.ToId);
				if (from is null || to is null)
					continue;

				var route = routes[edge];
				var laneY = route switch
				{
					EdgeRoute.GlobalLane => contentBottom + LaneGap * (globalLanes[edge] + 1),
					EdgeRoute.LocalLane => LocalLaneY(from, to),
					_ => 0,
				};

				laid.Add(new EdgeLayout(edge, BuildPath(route, (start.X, start.Y), (end.X, end.Y), laneY, edge.Kind)));
			}

			_edgeLayouts = laid;
			_portViews = BuildPortViews(anchors);
		}

		/// <summary>
		/// Groups each node's declared ports by the kind of the one edge that uses them, then stacks each group
		/// on its own side/band: Display inputs top-left, Display outputs top-right, Control outputs
		/// bottom-left, Control inputs bottom-right.
		/// </summary>
		private Dictionary<PortKey, PortAnchor> BuildPortAnchors()
		{
			var anchors = new Dictionary<PortKey, PortAnchor>();

			foreach (var node in Nodes)
			{
				var displayIn = new List<string>();
				var controlIn = new List<string>();
				foreach (var port in node.Inputs)
					(PortKind(node.Id, port.Label, isOutput: false) == TradeMapSignalKind.Display ? displayIn : controlIn).Add(port.Label);

				var displayOut = new List<string>();
				var controlOut = new List<string>();
				foreach (var port in node.Outputs)
					(PortKind(node.Id, port.Label, isOutput: true) == TradeMapSignalKind.Display ? displayOut : controlOut).Add(port.Label);

				for (var i = 0; i < displayIn.Count; i++)
					anchors[new PortKey(node.Id, displayIn[i], false)] = new PortAnchor(node.X, node.Y + TopCenterY(i), TradeMapSignalKind.Display);

				for (var i = 0; i < displayOut.Count; i++)
					anchors[new PortKey(node.Id, displayOut[i], true)] = new PortAnchor(node.X + node.W, node.Y + TopCenterY(i), TradeMapSignalKind.Display);

				for (var i = 0; i < controlOut.Count; i++)
					anchors[new PortKey(node.Id, controlOut[i], true)] = new PortAnchor(node.X, node.Y + BottomCenterY(node, i), TradeMapSignalKind.Control);

				for (var i = 0; i < controlIn.Count; i++)
					anchors[new PortKey(node.Id, controlIn[i], false)] = new PortAnchor(node.X + node.W, node.Y + BottomCenterY(node, i), TradeMapSignalKind.Control);
			}

			return anchors;
		}

		private Dictionary<PortKey, TradeMapPortView> BuildPortViews(IReadOnlyDictionary<PortKey, PortAnchor> anchors)
		{
			var views = new Dictionary<PortKey, TradeMapPortView>();

			foreach (var node in Nodes)
			{
				foreach (var port in node.Inputs)
				{
					if (!anchors.TryGetValue(new PortKey(node.Id, port.Label, false), out var anchor))
						continue;

					var sources = Edges
						.Where(e => e.ToId == node.Id && e.ToPort == port.Label)
						.Select(e => FindNode(e.FromId)?.Title)
						.Where(title => title is not null)
						.Distinct()
						.ToList();

					views[new PortKey(node.Id, port.Label, false)] = new TradeMapPortView(
						port.Label,
						anchor.Kind,
						IsOutput: false,
						anchor.Y - node.Y,
						sources.Count > 0,
						sources.Count > 0 ? $"IN — {port.Label}\nfrom {string.Join(", ", sources)}" : $"IN — {port.Label}\nunresolved"
					);
				}

				foreach (var port in node.Outputs)
				{
					if (!anchors.TryGetValue(new PortKey(node.Id, port.Label, true), out var anchor))
						continue;

					var targets = Edges
						.Where(e => e.FromId == node.Id && e.FromPort == port.Label)
						.Select(e => FindNode(e.ToId)?.Title)
						.Where(title => title is not null)
						.Distinct()
						.ToList();

					views[new PortKey(node.Id, port.Label, true)] = new TradeMapPortView(
						port.Label,
						anchor.Kind,
						IsOutput: true,
						anchor.Y - node.Y,
						targets.Count > 0,
						targets.Count > 0 ? $"OUT — {port.Label}\nto {string.Join(", ", targets)}" : $"OUT — {port.Label}\nunresolved"
					);
				}
			}

			return views;
		}

		private IReadOnlyList<TradeMapPortView> PortsFor(TradeMapNode node)
		{
			var views = new List<TradeMapPortView>(node.Inputs.Count + node.Outputs.Count);
			foreach (var port in node.Inputs)
				if (_portViews.TryGetValue(new PortKey(node.Id, port.Label, false), out var view))
					views.Add(view);
			foreach (var port in node.Outputs)
				if (_portViews.TryGetValue(new PortKey(node.Id, port.Label, true), out var view))
					views.Add(view);
			return views;
		}

		private TradeMapSignalKind PortKind(string nodeId, string label, bool isOutput)
		{
			var edge = isOutput
				? Edges.FirstOrDefault(e => e.FromId == nodeId && e.FromPort == label)
				: Edges.FirstOrDefault(e => e.ToId == nodeId && e.ToPort == label);
			return edge?.Kind ?? TradeMapSignalKind.Display;
		}

		private static double TopCenterY(int index) => HeaderHeight + PortsPad + index * PortRowHeight + PortRowHeight / 2;

		private static double BottomCenterY(TradeMapNode node, int index) => node.H - PortsPad - index * PortRowHeight - PortRowHeight / 2;

		private static double LocalLaneY(TradeMapNode from, TradeMapNode to) =>
			to.Y > from.Y ? (from.Y + from.H + to.Y) / 2 : (to.Y + to.H + from.Y) / 2;

		private TradeMapNode? FindNode(string nodeId) => Nodes.FirstOrDefault(n => n.Id == nodeId);

		/// <summary>
		/// A forward hop (Display into a later column) or a backward hop (Control into an earlier one) has its
		/// ports already facing each other, so it curves directly. Anything else -- same column, or a signal
		/// running against its own kind's grain -- has to loop through a lane instead.
		/// </summary>
		private EdgeRoute ClassifyRoute(TradeMapEdge edge)
		{
			var from = FindNode(edge.FromId);
			var to = FindNode(edge.ToId);
			if (from is null || to is null)
				return EdgeRoute.Bezier;

			var span = to.Column - from.Column;
			if (span == 0)
				return Math.Abs(to.Row - from.Row) <= 1 ? EdgeRoute.LocalLane : EdgeRoute.GlobalLane;

			var expectedSign = edge.Kind == TradeMapSignalKind.Display ? 1 : -1;
			if (Math.Sign(span) != expectedSign)
				return EdgeRoute.GlobalLane;

			return Math.Abs(span) > LongSpanThreshold ? EdgeRoute.GlobalLane : EdgeRoute.Bezier;
		}

		private Dictionary<TradeMapEdge, int> AssignLanes(IReadOnlyDictionary<TradeMapEdge, EdgeRoute> routes)
		{
			var lanes = new Dictionary<TradeMapEdge, int>();
			var ordered = routes.Where(pair => pair.Value == EdgeRoute.GlobalLane).Select(pair => pair.Key).OrderBy(ColumnSpan).ToList();

			for (var index = 0; index < ordered.Count; index++)
				lanes[ordered[index]] = index;

			return lanes;
		}

		private int ColumnSpan(TradeMapEdge edge)
		{
			var from = FindNode(edge.FromId);
			var to = FindNode(edge.ToId);
			return from is null || to is null ? 0 : Math.Abs(to.Column - from.Column);
		}

		private static string BuildPath(EdgeRoute route, (double X, double Y) start, (double X, double Y) end, double laneY, TradeMapSignalKind kind)
		{
			if (route == EdgeRoute.Bezier)
			{
				var direction = Math.Sign(end.X - start.X);
				if (direction == 0)
					direction = kind == TradeMapSignalKind.Display ? 1 : -1;

				var dx = Math.Max(46, Math.Abs(end.X - start.X) * 0.55) * direction;
				var c1 = (X: start.X + dx, Y: start.Y);
				var c2 = (X: end.X - dx, Y: end.Y);
				var tip = PullBack(c2, end);

				return string.Create(
					CultureInfo.InvariantCulture,
					$"M {start.X:0.##} {start.Y:0.##} C {c1.X:0.##} {c1.Y:0.##}, {c2.X:0.##} {c2.Y:0.##}, {tip.X:0.##} {tip.Y:0.##}"
				);
			}

			// A Display port exits/enters toward increasing X (forward); a Control port toward decreasing X
			// (backward) -- the stub direction has to match whichever side the port actually renders on.
			var stub = kind == TradeMapSignalKind.Display ? LaneStub : -LaneStub;
			var outX = start.X + stub;
			var inX = end.X - stub;
			return BuildOrthogonalPath([start, (outX, start.Y), (outX, laneY), (inX, laneY), (inX, end.Y), PullBack((inX, end.Y), end)]);
		}

		private static (double X, double Y) PullBack((double X, double Y) from, (double X, double Y) target)
		{
			var dx = target.X - from.X;
			var dy = target.Y - from.Y;
			var length = Math.Sqrt(dx * dx + dy * dy);
			return length < PortClearance ? target : (target.X - dx / length * PortClearance, target.Y - dy / length * PortClearance);
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
				builder.Append(CultureInfo.InvariantCulture, $" Q {corner.X:0.##} {corner.Y:0.##}, {exit.X:0.##} {exit.Y:0.##}");
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
			return length <= 0 ? from : (from.X + (to.X - from.X) / length * distance, from.Y + (to.Y - from.Y) / length * distance);
		}

		private bool IsVisible(TradeMapPartKind kind) => VisibleKinds is null || VisibleKinds.Count == 0 || VisibleKinds.Contains(kind);

		private bool IsDimmed(string nodeId) => HighlightedNodeIds is { Count: > 0 } && !HighlightedNodeIds.Contains(nodeId);

		private string NodeWrapperClass(TradeMapNode node) => IsVisible(node.Kind) ? "tmc-node" : "tmc-node tmc-node-hidden";

		private static string NodeStyle(TradeMapNode node) =>
			string.Create(CultureInfo.InvariantCulture, $"left:{node.X:0.##}px;top:{node.Y:0.##}px;width:{node.W:0.##}px;height:{node.H:0.##}px;");

		private string EdgeClass(EdgeLayout layout)
		{
			var kind = layout.Edge.Kind == TradeMapSignalKind.Control ? "control" : "display";
			var dimmed = IsEdgeDimmed(layout.Edge) ? " tmc-dimmed" : string.Empty;
			var hidden = IsEdgeVisible(layout.Edge) ? string.Empty : " tmc-edge-hidden";
			return $"tmc-edge tmc-edge-{kind}{dimmed}{hidden}";
		}

		private bool IsEdgeDimmed(TradeMapEdge edge) =>
			HighlightedNodeIds is { Count: > 0 } highlighted && !(highlighted.Contains(edge.FromId) && highlighted.Contains(edge.ToId));

		private bool IsEdgeVisible(TradeMapEdge edge)
		{
			var from = FindNode(edge.FromId);
			var to = FindNode(edge.ToId);
			return from is not null && to is not null && IsVisible(from.Kind) && IsVisible(to.Kind);
		}

		private static string EdgeTooltip(TradeMapEdge edge) => $"{edge.FromPort}  ->  {edge.ToPort}";

		private string CanvasStyle =>
			string.Create(CultureInfo.InvariantCulture, $"width:{_canvasWidth:0.##}px;height:{_canvasHeight:0.##}px;transform:scale({Zoom:0.###});");

		private string SpaceStyle =>
			string.Create(CultureInfo.InvariantCulture, $"width:{_canvasWidth * Zoom:0.##}px;height:{_canvasHeight * Zoom:0.##}px;");

		private string ViewBox => string.Create(CultureInfo.InvariantCulture, $"0 0 {_canvasWidth:0.##} {_canvasHeight:0.##}");

		private Task RaiseNodeSelected(string nodeId) => OnNodeSelected.InvokeAsync(nodeId);

		private Task RaiseBackgroundClicked() => OnBackgroundClicked.InvokeAsync();

		private Task HandleWheel(WheelEventArgs e) => OnWheelZoom.InvokeAsync(e.DeltaY);
	}
}
