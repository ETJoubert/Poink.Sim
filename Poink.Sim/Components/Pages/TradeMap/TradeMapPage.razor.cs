using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Poink.Sim.Components.Pages.TradeMap.Helpers;
using Poink.Sim.Components.Pages.TradeMap.Models;

namespace Poink.Sim.Components.Pages.TradeMap
{
	/// <summary>
	/// Hand-authored architecture map of the live Trade page (/trade), not derived from reflection like
	/// Code/Arch/Calls -- every node, port and connector below is placed and named by hand against the actual
	/// TradePage/SymbolWatchlistPanel/TradeChartPanel/FinancialsPanel/PositionTable/TradeControlsPanel source.
	/// </summary>
	public partial class TradeMapPage : ComponentBase
	{
		[Inject]
		private ILogger<TradeMapPage> Logger { get; set; } = default!;

		private const double MinZoom = 0.3;
		private const double MaxZoom = 1.5;
		private const double ZoomStep = 0.05;
		private const double DefaultZoom = 0.6;

		private string? _selectedId;
		private HashSet<string> _highlighted = [];
		private HashSet<TradeMapPartKind> _visibleKinds = [];
		private double _zoom = DefaultZoom;

		private static readonly TradeMapPartKind[] AllKinds = Enum.GetValues<TradeMapPartKind>();

		protected override void OnInitialized()
		{
			_visibleKinds = [.. AllKinds];

			Logger.LogInformation(
				"Trade map opened: {NodeCount} parts, {PortCount} ports, {EdgeCount} connectors",
				Nodes.Count,
				Nodes.Sum(n => n.Inputs.Count + n.Outputs.Count),
				Edges.Count
			);

			foreach (var unresolved in FindUnresolvedPorts())
				Logger.LogWarning("Trade map connector does not resolve to a declared port: {Wire}", unresolved);
		}

		private static IEnumerable<string> FindUnresolvedPorts()
		{
			foreach (var edge in Edges)
			{
				var from = Nodes.FirstOrDefault(n => n.Id == edge.FromId);
				var to = Nodes.FirstOrDefault(n => n.Id == edge.ToId);

				if (from is null || !from.Outputs.Any(p => p.Label == edge.FromPort))
					yield return $"{edge.FromId}.{edge.FromPort} -> {edge.ToId}.{edge.ToPort} (output missing)";
				else if (to is null || !to.Inputs.Any(p => p.Label == edge.ToPort))
					yield return $"{edge.FromId}.{edge.FromPort} -> {edge.ToId}.{edge.ToPort} (input missing)";
			}
		}

		private TradeMapNode? SelectedNode => _selectedId is null ? null : Nodes.FirstOrDefault(n => n.Id == _selectedId);

		private void SelectNode(string nodeId)
		{
			if (_selectedId == nodeId)
			{
				ClearSelection();
				return;
			}

			_selectedId = nodeId;
			_highlighted = BuildConnectorClosure(nodeId);

			Logger.LogInformation(
				"Trade map traced {NodeId}: {HighlightedCount} parts on its connector path",
				nodeId,
				_highlighted.Count
			);
		}

		private void ClearSelection()
		{
			_selectedId = null;
			_highlighted = [];
		}

		private static HashSet<string> BuildConnectorClosure(string nodeId)
		{
			var traced = new HashSet<string> { nodeId };

			Walk(nodeId, id => Edges.Where(e => e.ToId == id).Select(e => e.FromId));
			Walk(nodeId, id => Edges.Where(e => e.FromId == id).Select(e => e.ToId));

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

		private bool IsKindVisible(TradeMapPartKind kind) => _visibleKinds.Contains(kind);

		private void ToggleKind(TradeMapPartKind kind)
		{
			if (!_visibleKinds.Remove(kind))
				_visibleKinds.Add(kind);

			if (_visibleKinds.Count == 0)
				_visibleKinds = [.. AllKinds];
		}

		private static string KindLabel(TradeMapPartKind kind) => TradeMapKindHelper.Label(kind);

		private static string LegendClass(TradeMapPartKind kind) => $"tm-legend-{TradeMapKindHelper.Slug(kind)}";

		private static string LegendTooltip(TradeMapPartKind kind) =>
			kind switch
			{
				TradeMapPartKind.Page => "Page: the routable component that owns layout and cross-panel state. Click to show or hide.",
				TradeMapPartKind.Component => "Component: a UI panel rendered by the page. Click to show or hide.",
				TradeMapPartKind.Service => "Service: an injected dependency the page or its panels call into. Click to show or hide.",
				TradeMapPartKind.Helper => "Helper: a static, pure calculation function with no dependencies. Click to show or hide.",
				TradeMapPartKind.ExternalApi => "External API: the cTrader Open API boundary. Click to show or hide.",
				TradeMapPartKind.DataStore => "Data store: the Postgres-backed EF Core context. Click to show or hide.",
				_ => kind.ToString(),
			};

		private void ZoomIn() => _zoom = Math.Min(MaxZoom, Math.Round(_zoom + ZoomStep, 2));

		private void ZoomOut() => _zoom = Math.Max(MinZoom, Math.Round(_zoom - ZoomStep, 2));

		private void HandleWheelZoom(double deltaY)
		{
			if (deltaY < 0)
				ZoomIn();
			else if (deltaY > 0)
				ZoomOut();
		}

		private void ResetView()
		{
			_zoom = DefaultZoom;
			_visibleKinds = [.. AllKinds];
			ClearSelection();
		}

		private string ZoomLabel => (_zoom * 100).ToString("0", CultureInfo.InvariantCulture) + "%";

		// Column/Row group nodes for connector routing only (adjacent-column forward hops curve directly,
		// backward or same-column hops loop through a routed lane) -- X/Y/W/H below are the actual hand-placed
		// card geometry and are independent of this grouping.
		private static readonly IReadOnlyList<TradeMapNode> Nodes = new List<TradeMapNode>
		{
			new(
				"api",
				"cTrader Open API",
				TradeMapPartKind.ExternalApi,
				Column: 0,
				Row: 0,
				X: 40,
				Y: 60,
				W: 240,
				H: 200,
				Detail:
				[
					"_client: CTraderClient (trading, quotes, history)",
					"_app: OpenAPI.Net.Auth.App (auth code exchange only)",
					"Messages: ProtoOASpotEvent, ProtoOATraderUpdatedEvent,",
					"ProtoOAExecutionEvent",
				],
				Inputs: [new TradeMapPort("trading calls")],
				Outputs: [new TradeMapPort("message stream")]
			),
			new(
				"db",
				"PoinkSimContext",
				TradeMapPartKind.DataStore,
				Column: 0,
				Row: 1,
				X: 40,
				Y: 320,
				W: 240,
				H: 130,
				Detail:
				[
					"EF Core (Npgsql / Postgres)",
					"Symbols, SymbolMetadata, SymbolCategories,",
					"SymbolScheduleIntervals, SymbolHolidays",
				],
				Inputs: [new TradeMapPort("EF read/write")],
				Outputs: []
			),
			new(
				"session",
				"ICTraderSessionService",
				TradeMapPartKind.Service,
				Column: 1,
				Row: 0,
				X: 420,
				Y: 60,
				W: 260,
				H: 220,
				Detail:
				[
					"impl: CTraderSessionService (singleton)",
					"also implements ICTraderHistory",
					"owns Watchlist, Quotes, Positions, State,",
					"Accounts, Balance, UseLiveEnvironment",
				],
				Inputs:
				[
					new TradeMapPort("message stream"),
					new TradeMapPort("symbol data calls"),
					new TradeMapPort("watch calls"),
					new TradeMapPort("tick history calls"),
					new TradeMapPort("order calls"),
					new TradeMapPort("session calls"),
				],
				Outputs:
				[
					new TradeMapPort("trading calls"),
					new TradeMapPort("watchlist state"),
					new TradeMapPort("tick stream"),
					new TradeMapPort("connection state"),
				]
			),
			new(
				"symsvc",
				"ISymbolService",
				TradeMapPartKind.Service,
				Column: 1,
				Row: 1,
				X: 420,
				Y: 340,
				W: 260,
				H: 140,
				Detail: ["impl: SymbolService (scoped)", "wraps ICTraderSessionService + PoinkSimContext"],
				Inputs: [new TradeMapPort("metadata lookup"), new TradeMapPort("category calls")],
				Outputs: [new TradeMapPort("EF read/write"), new TradeMapPort("symbol data calls")]
			),
			new(
				"metacache",
				"ISymbolMetadataCache",
				TradeMapPartKind.Service,
				Column: 1,
				Row: 2,
				X: 420,
				Y: 540,
				W: 260,
				H: 130,
				Detail: ["impl: SymbolMetadataCacheService (scoped)", "in-memory cache over ISymbolService"],
				Inputs: [new TradeMapPort("ensure metadata")],
				Outputs: [new TradeMapPort("metadata lookup"), new TradeMapPort("metadata snapshot")]
			),
			new(
				"filterhelper",
				"SymbolFilterHelper",
				TradeMapPartKind.Helper,
				Column: 1,
				Row: 3,
				X: 420,
				Y: 730,
				W: 120,
				H: 80,
				Detail: ["static · pure", "used by Watchlist"],
				Inputs: [],
				Outputs: []
			),
			new(
				"chartwindowhelper",
				"ChartWindowHelper",
				TradeMapPartKind.Helper,
				Column: 1,
				Row: 3,
				X: 560,
				Y: 730,
				W: 120,
				H: 80,
				Detail: ["static · pure", "used by Chart"],
				Inputs: [],
				Outputs: []
			),
			new(
				"pnlhelper",
				"PositionPnlHelper",
				TradeMapPartKind.Helper,
				Column: 1,
				Row: 4,
				X: 420,
				Y: 830,
				W: 120,
				H: 80,
				Detail: ["static · pure", "used by Financials + TradePage (PipSize)"],
				Inputs: [],
				Outputs: []
			),
			new(
				"riskhelper",
				"RiskHelper",
				TradeMapPartKind.Helper,
				Column: 1,
				Row: 4,
				X: 560,
				Y: 830,
				W: 120,
				H: 80,
				Detail: ["static · pure", "used by Controls"],
				Inputs: [],
				Outputs: []
			),
			new(
				"page",
				"TradePage",
				TradeMapPartKind.Page,
				Column: 2,
				Row: 0,
				X: 840,
				Y: 60,
				W: 300,
				H: 340,
				Detail:
				[
					"@page \"/trade\"",
					"injects: Session, MetadataCache, Navigation,",
					"JS, LocalStorage",
					"connector -- selection state + settings persistence",
				],
				Inputs:
				[
					new TradeMapPort("connection state"),
					new TradeMapPort("metadata snapshot"),
					new TradeMapPort("symbol selected"),
					new TradeMapPort("symbol unwatched"),
					new TradeMapPort("sidebar collapsed"),
					new TradeMapPort("chart settings snapshot"),
					new TradeMapPort("position selected"),
					new TradeMapPort("position closed"),
				],
				Outputs:
				[
					new TradeMapPort("session calls"),
					new TradeMapPort("sidebar collapsed"),
					new TradeMapPort("selected symbol"),
					new TradeMapPort("chart parameters"),
					new TradeMapPort("financials parameters"),
					new TradeMapPort("controls parameters"),
				]
			),
			new(
				"watchlist",
				"SymbolWatchlistPanel",
				TradeMapPartKind.Component,
				Column: 3,
				Row: 0,
				X: 1300,
				Y: 60,
				W: 340,
				H: 210,
				Detail:
				[
					"left sidebar, collapsible (@bind-Collapsed)",
					"injects: Session, SymbolService, MetadataCache",
					"calc: SymbolFilterHelper",
				],
				Inputs:
				[
					new TradeMapPort("watchlist state"),
					new TradeMapPort("sidebar collapsed"),
					new TradeMapPort("selected symbol"),
				],
				Outputs:
				[
					new TradeMapPort("watch calls"),
					new TradeMapPort("category calls"),
					new TradeMapPort("ensure metadata"),
					new TradeMapPort("symbol selected"),
					new TradeMapPort("symbol unwatched"),
					new TradeMapPort("sidebar collapsed"),
				]
			),
			new(
				"chart",
				"TradeChartPanel",
				TradeMapPartKind.Component,
				Column: 3,
				Row: 1,
				X: 1300,
				Y: 320,
				W: 340,
				H: 180,
				Detail:
				[
					"candles, XGrid, volume profile",
					"injects: Session, History (ICTraderHistory), JS",
					"calc: ChartWindowHelper",
				],
				Inputs: [new TradeMapPort("tick stream"), new TradeMapPort("chart parameters")],
				Outputs: [new TradeMapPort("tick history calls"), new TradeMapPort("chart settings snapshot")]
			),
			new(
				"financials",
				"FinancialsPanel",
				TradeMapPartKind.Component,
				Column: 3,
				Row: 2,
				X: 1300,
				Y: 550,
				W: 340,
				H: 150,
				Detail: ["renders PositionTable", "no injected services, pure props", "calc: PositionPnlHelper"],
				Inputs:
				[
					new TradeMapPort("financials parameters"),
					new TradeMapPort("row selected"),
					new TradeMapPort("row closed"),
				],
				Outputs:
				[
					new TradeMapPort("position selected"),
					new TradeMapPort("position closed"),
					new TradeMapPort("position rows"),
				]
			),
			new(
				"positiontable",
				"PositionTable",
				TradeMapPartKind.Component,
				Column: 3,
				Row: 3,
				X: 1340,
				Y: 740,
				W: 260,
				H: 120,
				Detail: ["row rendering, nested in Financials", "calc: PositionPnlHelper (PipDifference, UnrealizedPnl)"],
				Inputs: [new TradeMapPort("position rows")],
				Outputs: [new TradeMapPort("row selected"), new TradeMapPort("row closed")]
			),
			new(
				"controls",
				"TradeControlsPanel",
				TradeMapPartKind.Component,
				Column: 3,
				Row: 4,
				X: 1300,
				Y: 910,
				W: 340,
				H: 190,
				Detail: ["right sidebar", "injects: Session, JS", "calc: RiskHelper"],
				Inputs: [new TradeMapPort("controls parameters")],
				Outputs: [new TradeMapPort("order calls")]
			),
		};

		private static readonly IReadOnlyList<TradeMapEdge> Edges = new List<TradeMapEdge>
		{
			new("session", "trading calls", "api", "trading calls", TradeMapSignalKind.Control),
			new("api", "message stream", "session", "message stream", TradeMapSignalKind.Display),
			new("symsvc", "EF read/write", "db", "EF read/write", TradeMapSignalKind.Control),
			new("symsvc", "symbol data calls", "session", "symbol data calls", TradeMapSignalKind.Control),
			new("watchlist", "watch calls", "session", "watch calls", TradeMapSignalKind.Control),
			new("chart", "tick history calls", "session", "tick history calls", TradeMapSignalKind.Control),
			new("controls", "order calls", "session", "order calls", TradeMapSignalKind.Control),
			new("page", "session calls", "session", "session calls", TradeMapSignalKind.Control),
			new("session", "watchlist state", "watchlist", "watchlist state", TradeMapSignalKind.Display),
			new("session", "tick stream", "chart", "tick stream", TradeMapSignalKind.Display),
			new("session", "connection state", "page", "connection state", TradeMapSignalKind.Display),
			new("metacache", "metadata lookup", "symsvc", "metadata lookup", TradeMapSignalKind.Control),
			new("watchlist", "category calls", "symsvc", "category calls", TradeMapSignalKind.Control),
			new("watchlist", "ensure metadata", "metacache", "ensure metadata", TradeMapSignalKind.Control),
			new("metacache", "metadata snapshot", "page", "metadata snapshot", TradeMapSignalKind.Display),
			new("watchlist", "symbol selected", "page", "symbol selected", TradeMapSignalKind.Control),
			new("watchlist", "symbol unwatched", "page", "symbol unwatched", TradeMapSignalKind.Control),
			new("watchlist", "sidebar collapsed", "page", "sidebar collapsed", TradeMapSignalKind.Display),
			new("page", "sidebar collapsed", "watchlist", "sidebar collapsed", TradeMapSignalKind.Display),
			new("page", "selected symbol", "watchlist", "selected symbol", TradeMapSignalKind.Display),
			new("page", "chart parameters", "chart", "chart parameters", TradeMapSignalKind.Display),
			new("chart", "chart settings snapshot", "page", "chart settings snapshot", TradeMapSignalKind.Display),
			new("page", "financials parameters", "financials", "financials parameters", TradeMapSignalKind.Display),
			new("financials", "position selected", "page", "position selected", TradeMapSignalKind.Control),
			new("financials", "position closed", "page", "position closed", TradeMapSignalKind.Control),
			new("page", "controls parameters", "controls", "controls parameters", TradeMapSignalKind.Display),
			new("financials", "position rows", "positiontable", "position rows", TradeMapSignalKind.Display),
			new("positiontable", "row selected", "financials", "row selected", TradeMapSignalKind.Control),
			new("positiontable", "row closed", "financials", "row closed", TradeMapSignalKind.Control),
		};
	}
}
