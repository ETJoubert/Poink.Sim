using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Poink.Sim.Brain.Neural;

namespace Poink.Sim.Components.Training
{
	/// <summary>
	/// Live render of one NEAT genome's graph: Input/Bias anchor left, Output anchors right,
	/// and Hidden nodes flow between them by topological depth. Drawn on &lt;canvas&gt; via JS
	/// interop, same pattern as XGrid/MarketChart/CandlestickChart. Colors are passed as CSS
	/// custom-property names so the JS side stays theme-aware without duplicating hex values.
	/// </summary>
	public partial class NeatTopologyGraph : ComponentBase, IAsyncDisposable
	{
		private const double PadLeftPercent = 6;
		private const double PadRightPercent = 6;
		private const double PadTopPercent = 6;
		private const double PadBottomPercent = 6;

		private const double MinEdgeOpacity = 0.15;
		private const double MaxEdgeWeightForOpacity = 2.0;

		// Nodes in a depth column spread across the column's full height, shrinking diameter with
		// count instead of wrapping. ReferenceHeightPx stands in for the unmeasurable live DOM height.
		private const double ReferenceHeightPx = 170;
		private const double MaxNodeDiameterPx = 8;
		private const double MinNodeDiameterPx = 2.5;
		// diameter = spacing / this -- leaves a visible gap between adjacent circles.
		private const double NodeSpacingFactor = 1.6;

		// Hidden nodes flow between the fixed Input and Output columns, by depth; this margin
		// keeps that band clear of the two anchored columns rather than crowding right up to them.
		private const double HiddenBandMargin = 8;

		[Inject]
		private IJSRuntime JSRuntime { get; set; } = null!;

		[Parameter]
		public NeatTopologySnapshot? Topology { get; set; }

		/// <summary>
		/// Recipe (not live data) used to attribute each Input node index to the encoder that
		/// produced it, so Input nodes can be colored to match that encoder's card in the Source column.
		/// </summary>
		[Parameter]
		public BrainRecipe? Recipe { get; set; }

		// Fixed check order matching TrainingPreview.BuildSourceRows and CompositeEncoder's
		// actual concatenation order, not Recipe.EncoderKinds' own order.
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

		// Input/Bias and Output always anchor to fixed stacked columns regardless of topological
		// depth; only Hidden nodes lay out by depth, flowing through the band between them.
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

		// Input node Ids are assigned 0..InputCount-1 in this encoder order at genome creation
		// (NeatPopulation.CreateInitialGenomes), so a cumulative offset recovers the source encoder.
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
