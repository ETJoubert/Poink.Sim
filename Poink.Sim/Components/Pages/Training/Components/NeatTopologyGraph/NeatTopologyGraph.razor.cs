using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Poink.Sim.Brain.Neural;

namespace Poink.Sim.Components.Training
{
	// Live render of one NEAT genome's graph -- Input/Bias anchor to a stacked left column,
	// Output anchors to a stacked right column, and Hidden nodes flow between them by
	// topological depth. No windowing or rolling aggregation, since a genome is already a
	// single point-in-time structure.
	//
	// Drawn on <canvas> via JS interop, same pattern as XGrid/MarketChart/CandlestickChart
	// (Components/Charts/) -- lazily import a module in OnAfterRenderAsync, call its draw(canvas,
	// payload) export every render, dispose the module reference on teardown. Colors are passed
	// as CSS custom-property names, not resolved values, so the JS side reads them live via
	// getComputedStyle and stays theme-aware without duplicating hex colors into JS.
	public partial class NeatTopologyGraph : ComponentBase, IAsyncDisposable
	{
		private const double PadLeftPercent = 6;
		private const double PadRightPercent = 6;
		private const double PadTopPercent = 6;
		private const double PadBottomPercent = 6;

		private const double MinEdgeOpacity = 0.15;
		private const double MaxEdgeWeightForOpacity = 2.0;

		// Every node in a depth column (almost always Input/Bias -- dozens of raw encoder
		// features land at depth 0) is spread across the column's full height rather than
		// wrapping into a grid; diameter shrinks with node count instead, so a crowded column
		// stays legible without ever exceeding the container's own height. ReferenceHeightPx is
		// a heuristic stand-in for the container's actual rendered height (unknowable without
		// measuring the live DOM) -- it only affects how aggressively circles shrink under
		// crowding, not layout.
		private const double ReferenceHeightPx = 170;
		private const double MaxNodeDiameterPx = 8;
		private const double MinNodeDiameterPx = 2.5;
		private const double NodeSpacingFactor = 1.6; // diameter = spacing / this -- leaves a visible gap between adjacent circles

		// Hidden nodes flow between the fixed Input and Output columns, by depth; this margin
		// keeps that band clear of the two anchored columns rather than crowding right up to them.
		private const double HiddenBandMargin = 8;

		[Inject]
		private IJSRuntime JSRuntime { get; set; } = null!;

		[Parameter]
		public NeatTopologySnapshot? Topology { get; set; }

		// Recipe-only (not live data) -- used solely to attribute each Input node index to the
		// encoder that produced it, so Input nodes can be colored to match that encoder's card
		// in the Source column, same as TrainingPreview.SourceColorClass does there.
		[Parameter]
		public BrainRecipe? Recipe { get; set; }

		// Fixed check order, not Recipe.EncoderKinds' own order -- matches
		// TrainingPreview.BuildSourceRows, and both agree with CompositeEncoder's actual
		// concatenation order because BrainBuilder always assembles EncoderKinds in this order.
		private static readonly (EncoderKind Kind, string Label)[] CanonicalEncoderOrder =
		[
			(EncoderKind.BarWindow, "Bar Window"),
			(EncoderKind.VolumeProfile, "Volume Profile"),
			(EncoderKind.TimeProfile, "Time Profile"),
			(EncoderKind.SessionClock, "Session Clock"),
			(EncoderKind.SpreadVolatility, "Spread + Volatility"),
			(EncoderKind.TraderState, "Trader State"),
			(EncoderKind.XGrid, "XGrid Signals"),
		];

		private ElementReference _canvasRef;
		private IJSObjectReference? _module;
		private bool _disposed;

		private static double PlotHeightPercent => 100 - PadTopPercent - PadBottomPercent;

		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			try
			{
				_module ??= await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/neatTopologyGraph.js");
				if (_disposed || Topology is null || Topology.Nodes.Count == 0)
					return;
				await _module.InvokeVoidAsync("draw", _canvasRef, BuildPayload());
			}
			catch (JSDisconnectedException) { }
			catch (ObjectDisposedException) { }
		}

		public async ValueTask DisposeAsync()
		{
			_disposed = true;
			if (_module is not null)
			{
				try
				{
					await _module.DisposeAsync();
				}
				catch (JSDisconnectedException) { }
				catch (ObjectDisposedException) { }
			}
			GC.SuppressFinalize(this);
		}

		// Input/Bias always anchor to the left edge and Output always anchors to the right edge
		// -- both single stacked columns, regardless of topological depth, so an output reached
		// through a long mutation chain doesn't wander into the middle of the graph. Only Hidden
		// nodes lay out by depth, flowing through the band between the two anchored columns.
		private Dictionary<int, (double XPercent, double YPercent, double DiameterPx)> ComputePositions()
		{
			var positions = new Dictionary<int, (double, double, double)>();
			if (Topology is null || Topology.Nodes.Count == 0)
				return positions;

			double leftX = PadLeftPercent;
			double rightX = 100 - PadRightPercent;

			PlaceColumn(Topology.Nodes.Where(n => n.Kind is NeatNodeKind.Input or NeatNodeKind.Bias), leftX, positions);
			PlaceColumn(Topology.Nodes.Where(n => n.Kind == NeatNodeKind.Output), rightX, positions);

			var hiddenNodes = Topology.Nodes.Where(n => n.Kind == NeatNodeKind.Hidden).ToList();
			if (hiddenNodes.Count > 0)
			{
				int minDepth = hiddenNodes.Min(n => n.Depth);
				int maxDepth = hiddenNodes.Max(n => n.Depth);
				int span = Math.Max(1, maxDepth - minDepth);

				double bandStart = leftX + HiddenBandMargin;
				double bandEnd = rightX - HiddenBandMargin;
				if (bandEnd < bandStart)
					(bandStart, bandEnd) = ((leftX + rightX) / 2 - 1, (leftX + rightX) / 2 + 1);

				foreach (var depthGroup in hiddenNodes.GroupBy(n => n.Depth).OrderBy(g => g.Key))
				{
					double t = maxDepth == minDepth ? 0.5 : (depthGroup.Key - minDepth) / (double)span;
					double x = bandStart + t * (bandEnd - bandStart);
					PlaceColumn(depthGroup, x, positions);
				}
			}

			return positions;
		}

		private static void PlaceColumn(
			IEnumerable<NeatTopologyNode> nodes,
			double x,
			Dictionary<int, (double, double, double)> positions
		)
		{
			var ordered = nodes.OrderBy(n => n.Id).ToList();
			int count = ordered.Count;
			if (count == 0)
				return;

			double spacingPercent = count <= 1 ? PlotHeightPercent : PlotHeightPercent / (count - 1);
			double spacingPx = spacingPercent / 100 * ReferenceHeightPx;
			double diameter = Math.Clamp(spacingPx / NodeSpacingFactor, MinNodeDiameterPx, MaxNodeDiameterPx);

			for (int i = 0; i < count; i++)
			{
				double y = count == 1 ? PadTopPercent + PlotHeightPercent / 2 : PadTopPercent + i * spacingPercent;
				positions[ordered[i].Id] = (x, y, diameter);
			}
		}

		private string NodeColorToken(NeatNodeKind kind, int id, Dictionary<int, string> inputSourceBadges) =>
			kind switch
			{
				NeatNodeKind.Input => inputSourceBadges.GetValueOrDefault(id, "--color-neutral"),
				NeatNodeKind.Bias => "--color-neutral",
				NeatNodeKind.Output => "--color-success",
				_ => "--color-info",
			};

		private static string SourceBadgeToken(string source) =>
			source switch
			{
				"Bar Window" => "--color-badge-1",
				"Volume Profile" => "--color-badge-2",
				"Time Profile" => "--color-badge-3",
				"Session Clock" => "--color-badge-4",
				"Spread + Volatility" => "--color-badge-5",
				_ => "--color-neutral",
			};

		// Input node Ids are assigned 0..InputCount-1 in exactly this encoder order when the
		// genome is created (NeatPopulation.CreateInitialGenomes), so a cumulative offset over
		// CanonicalEncoderOrder recovers which encoder produced a given input index.
		private Dictionary<int, string> ComputeInputSourceBadges()
		{
			var badges = new Dictionary<int, string>();
			if (Recipe is null)
				return badges;

			int offset = 0;
			foreach (var (kind, label) in CanonicalEncoderOrder)
			{
				if (!Recipe.EncoderKinds.Contains(kind))
					continue;
				int count = BrainCatalog.CreateEncoder(kind, Recipe.EncoderSizes).FeatureCount;
				string colorToken = SourceBadgeToken(label);
				for (int id = offset; id < offset + count; id++)
					badges[id] = colorToken;
				offset += count;
			}
			return badges;
		}

		private object BuildPayload()
		{
			if (Topology is null)
				return new { nodes = Array.Empty<object>(), edges = Array.Empty<object>() };

			var positions = ComputePositions();
			var inputSourceBadges = ComputeInputSourceBadges();

			var nodes = Topology
				.Nodes.Select(n =>
				{
					var (x, y, diameter) = positions[n.Id];
					return new
					{
						x,
						y,
						d = diameter,
						color = NodeColorToken(n.Kind, n.Id, inputSourceBadges),
					};
				})
				.ToList();

			var edges = Topology
				.Connections.Where(c => positions.ContainsKey(c.FromNodeId) && positions.ContainsKey(c.ToNodeId))
				.Select(c =>
				{
					var (x1, y1, _) = positions[c.FromNodeId];
					var (x2, y2, _) = positions[c.ToNodeId];
					return new
					{
						x1,
						y1,
						x2,
						y2,
						enabled = c.Enabled,
						color = !c.Enabled ? "--color-muted"
						: c.Weight >= 0 ? "--color-success"
						: "--color-error",
						opacity = c.Enabled
							? Math.Clamp(Math.Abs(c.Weight) / MaxEdgeWeightForOpacity, MinEdgeOpacity, 1.0)
							: MinEdgeOpacity,
					};
				})
				.ToList();

			return new { nodes, edges };
		}

		private int NodeCount => Topology?.Nodes.Count ?? 0;
		private int EnabledConnectionCount => Topology?.Connections.Count(c => c.Enabled) ?? 0;

		private string StatusLabel => Topology is null ? "" : $"gen {Topology.Generation}";
	}
}
