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

		private TradeMapNode? SelectedNode =>
			_selectedId is null ? null : Nodes.FirstOrDefault(n => n.Id == _selectedId);

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
				TradeMapPartKind.Page =>
					"Page: the routable part that owns layout and cross-panel state. Click to show or hide.",
				TradeMapPartKind.Panel =>
					"Panel: a UI-rendering unit -- it can display and/or accept control input. Click to show or hide.",
				TradeMapPartKind.Driver =>
					"Driver: a narrow, single-consumer adapter to one external capability -- no cross-cutting knowledge. Click to show or hide.",
				TradeMapPartKind.Helper =>
					"Helper: a static, pure calculation function with no dependencies. Click to show or hide.",
				TradeMapPartKind.ExternalApi => "External API: the cTrader Open API boundary. Click to show or hide.",
				TradeMapPartKind.DataStore => "Data store: the Postgres-backed EF Core context. Click to show or hide.",
				TradeMapPartKind.UserSpace =>
					"User space: the buttons and inputs a person interacts with directly. Click to show or hide.",
				TradeMapPartKind.Coordinator =>
					"Coordinator: proposed orchestrator every panel would route through for cross-cutting concerns -- not in the current code. Click to show or hide.",
				TradeMapPartKind.Interface =>
					"Interface: a narrow, segregated contract onto one driver -- consumers depend on this, not the driver directly. Click to show or hide.",
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

		// Column/Row group nodes for connector routing only, independent of the hand-placed X/Y/W/H below --
		// forward/backward hops curve directly, same-column or against-the-grain hops loop through a lane.
		private static readonly IReadOnlyList<TradeMapNode> Nodes = new List<TradeMapNode>
		{
			// PROPOSED -- not the current implementation. Drawn first so it paints behind TradeCoordinator and
			// the panels, matching how it wraps them: a pure layout shell with no ports of its own.
			new(
				"tradepage",
				"TradePage",
				TradeMapPartKind.Page,
				Column: 3,
				Row: 0,
				X: 1160,
				Y: 20,
				W: 870,
				H: 1150,
				Detail:
				[
					"PROPOSED -- not the current implementation",
					"@page \"/trade\", pure layout container",
					"renders TradeCoordinator + the five panels",
					"no service injection, no direct signal wiring",
				],
				Inputs: [],
				Outputs: []
			),
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
				TradeMapPartKind.Driver,
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
					new TradeMapPort("position close calls"),
					new TradeMapPort("session lifecycle calls"),
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
				TradeMapPartKind.Driver,
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
				TradeMapPartKind.Driver,
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
				Detail: ["static · pure", "used by Financials + Controls (PipSize)"],
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
			// PROPOSED -- not in the current code (ICTraderHistory below is the one real exception). Each pill is
			// a narrow, segregated contract onto exactly one driver; panels depend on these, never the driver itself.
			new(
				"iaccount",
				"IAccountAccess",
				TradeMapPartKind.Interface,
				Column: 2,
				Row: 0,
				X: 820,
				Y: 600,
				W: 240,
				H: 110,
				Detail: ["PROPOSED -- not in the current code", "connection/account state + session lifecycle"],
				Inputs: [new TradeMapPort("connection state"), new TradeMapPort("session lifecycle calls")],
				Outputs: [new TradeMapPort("connection state"), new TradeMapPort("session lifecycle calls")]
			),
			new(
				"iwatchlist",
				"IWatchlistAccess",
				TradeMapPartKind.Interface,
				Column: 2,
				Row: 1,
				X: 820,
				Y: 60,
				W: 240,
				H: 110,
				Detail: ["PROPOSED -- not in the current code", "watch/unwatch + live watchlist state"],
				Inputs: [new TradeMapPort("watch calls"), new TradeMapPort("watchlist state")],
				Outputs: [new TradeMapPort("watch calls"), new TradeMapPort("watchlist state")]
			),
			new(
				"ihistory",
				"ICTraderHistory",
				TradeMapPartKind.Interface,
				Column: 2,
				Row: 2,
				X: 820,
				Y: 195,
				W: 240,
				H: 110,
				Detail: ["REAL -- already segregated today", "tick history lookups"],
				Inputs: [new TradeMapPort("tick history calls")],
				Outputs: [new TradeMapPort("tick history calls")]
			),
			new(
				"itickstream",
				"ITickStreamAccess",
				TradeMapPartKind.Interface,
				Column: 2,
				Row: 3,
				X: 820,
				Y: 330,
				W: 240,
				H: 110,
				Detail: ["PROPOSED -- not in the current code", "live quotes, positions, balance"],
				Inputs: [new TradeMapPort("tick stream")],
				Outputs: [new TradeMapPort("tick stream")]
			),
			new(
				"itradeexec",
				"ITradeExecutionAccess",
				TradeMapPartKind.Interface,
				Column: 2,
				Row: 4,
				X: 820,
				Y: 465,
				W: 240,
				H: 110,
				Detail: ["PROPOSED -- not in the current code", "place/close orders, close positions"],
				Inputs: [new TradeMapPort("order calls"), new TradeMapPort("position close calls")],
				Outputs: [new TradeMapPort("order calls"), new TradeMapPort("position close calls")]
			),
			new(
				"icategory",
				"ICategoryAccess",
				TradeMapPartKind.Interface,
				Column: 2,
				Row: 5,
				X: 820,
				Y: 735,
				W: 240,
				H: 110,
				Detail: ["PROPOSED -- not in the current code", "symbol category lookups"],
				Inputs: [new TradeMapPort("category calls")],
				Outputs: [new TradeMapPort("category calls")]
			),
			new(
				"imetaensure",
				"IMetadataEnsureAccess",
				TradeMapPartKind.Interface,
				Column: 2,
				Row: 6,
				X: 820,
				Y: 870,
				W: 240,
				H: 110,
				Detail: ["PROPOSED -- not in the current code", "ensure metadata cached for a symbol"],
				Inputs: [new TradeMapPort("ensure metadata")],
				Outputs: [new TradeMapPort("ensure metadata")]
			),
			new(
				"imetasnapshot",
				"IMetadataSnapshotAccess",
				TradeMapPartKind.Interface,
				Column: 2,
				Row: 7,
				X: 820,
				Y: 1005,
				W: 240,
				H: 110,
				Detail: ["PROPOSED -- not in the current code", "read-only metadata snapshot dictionary"],
				Inputs: [new TradeMapPort("metadata snapshot")],
				Outputs: [new TradeMapPort("metadata snapshot")]
			),
			// PROPOSED -- not the current implementation. Slimmed to only what has no driver interface of its
			// own: cross-panel UI state (selected symbol, sidebar, chart settings persistence).
			new(
				"tradecoordinator",
				"TradeCoordinator",
				TradeMapPartKind.Coordinator,
				Column: 3,
				Row: 0,
				X: 1200,
				Y: 60,
				W: 300,
				H: 260,
				Detail:
				[
					"PROPOSED coordinator -- not in the current code",
					"owns only cross-panel UI state, not driver access",
					"selected symbol, sidebar collapsed, settings persistence",
					"panels reach drivers via Interface ports directly",
				],
				Inputs:
				[
					new TradeMapPort("connection state"),
					new TradeMapPort("symbol selected"),
					new TradeMapPort("symbol unwatched"),
					new TradeMapPort("sidebar collapsed"),
					new TradeMapPort("chart settings snapshot"),
					new TradeMapPort("position selected"),
				],
				Outputs:
				[
					new TradeMapPort("session lifecycle calls"),
					new TradeMapPort("sidebar collapsed"),
					new TradeMapPort("selected symbol"),
					new TradeMapPort("chart parameters"),
				]
			),
			new(
				"watchlist",
				"SymbolWatchlistPanel",
				TradeMapPartKind.Panel,
				Column: 4,
				Row: 0,
				X: 1650,
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
					new TradeMapPort("user input"),
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
				TradeMapPartKind.Panel,
				Column: 4,
				Row: 1,
				X: 1650,
				Y: 320,
				W: 340,
				H: 180,
				Detail:
				[
					"candles, XGrid, volume profile",
					"injects: Session, History (ICTraderHistory), JS",
					"calc: ChartWindowHelper",
				],
				Inputs:
				[
					new TradeMapPort("tick stream"),
					new TradeMapPort("chart parameters"),
					new TradeMapPort("user input"),
				],
				Outputs: [new TradeMapPort("tick history calls"), new TradeMapPort("chart settings snapshot")]
			),
			new(
				"financials",
				"FinancialsPanel",
				TradeMapPartKind.Panel,
				Column: 4,
				Row: 2,
				X: 1650,
				Y: 550,
				W: 340,
				H: 170,
				Detail: ["renders PositionTable", "no injected services, pure props", "calc: PositionPnlHelper"],
				Inputs:
				[
					new TradeMapPort("tick stream"),
					new TradeMapPort("metadata snapshot"),
					new TradeMapPort("row selected"),
					new TradeMapPort("row closed"),
				],
				Outputs:
				[
					new TradeMapPort("position selected"),
					new TradeMapPort("position close calls"),
					new TradeMapPort("position rows"),
				]
			),
			new(
				"positiontable",
				"PositionTable",
				TradeMapPartKind.Panel,
				Column: 4,
				Row: 3,
				X: 1690,
				Y: 760,
				W: 260,
				H: 120,
				Detail:
				[
					"row rendering, nested in Financials",
					"calc: PositionPnlHelper (PipDifference, UnrealizedPnl)",
				],
				Inputs: [new TradeMapPort("position rows"), new TradeMapPort("user input")],
				Outputs: [new TradeMapPort("row selected"), new TradeMapPort("row closed")]
			),
			new(
				"controls",
				"TradeControlsPanel",
				TradeMapPartKind.Panel,
				Column: 4,
				Row: 4,
				X: 1650,
				Y: 930,
				W: 340,
				H: 190,
				Detail: ["right sidebar", "injects: Session, JS", "calc: RiskHelper"],
				Inputs:
				[
					new TradeMapPort("selected symbol"),
					new TradeMapPort("tick stream"),
					new TradeMapPort("metadata snapshot"),
					new TradeMapPort("user input"),
				],
				Outputs: [new TradeMapPort("order calls")]
			),
			new(
				"userspace-watchlist",
				"Watchlist Controls",
				TradeMapPartKind.UserSpace,
				Column: 5,
				Row: 0,
				X: 2130,
				Y: 60,
				W: 280,
				H: 140,
				Detail: ["search filter, category/FX toggles,", "watch/unwatch buttons, collapse toggle"],
				Inputs: [],
				Outputs: [new TradeMapPort("user input")]
			),
			new(
				"userspace-chart",
				"Chart Controls",
				TradeMapPartKind.UserSpace,
				Column: 5,
				Row: 1,
				X: 2130,
				Y: 320,
				W: 280,
				H: 140,
				Detail: ["bar size buttons, profile lines toggle"],
				Inputs: [],
				Outputs: [new TradeMapPort("user input")]
			),
			new(
				"userspace-positiontable",
				"Position Row Actions",
				TradeMapPartKind.UserSpace,
				Column: 5,
				Row: 3,
				X: 2130,
				Y: 760,
				W: 280,
				H: 120,
				Detail: ["select row, close position button"],
				Inputs: [],
				Outputs: [new TradeMapPort("user input")]
			),
			new(
				"userspace-controls",
				"Order Controls",
				TradeMapPartKind.UserSpace,
				Column: 5,
				Row: 4,
				X: 2130,
				Y: 930,
				W: 280,
				H: 190,
				Detail: ["volume, SL/TP inputs, order type,", "Buy / Sell buttons"],
				Inputs: [],
				Outputs: [new TradeMapPort("user input")]
			),
		};

		private static readonly IReadOnlyList<TradeMapEdge> Edges = new List<TradeMapEdge>
		{
			new("session", "trading calls", "api", "trading calls", TradeMapSignalKind.Control),
			new("api", "message stream", "session", "message stream", TradeMapSignalKind.Display),
			new("symsvc", "EF read/write", "db", "EF read/write", TradeMapSignalKind.Control),
			new("symsvc", "symbol data calls", "session", "symbol data calls", TradeMapSignalKind.Control),
			new("metacache", "metadata lookup", "symsvc", "metadata lookup", TradeMapSignalKind.Control),
			// PROPOSED -- every panel-to-driver signal now runs through a segregated Interface pill instead of
			// going direct. Each pill relays exactly one narrow slice of one driver.
			new("watchlist", "watch calls", "iwatchlist", "watch calls", TradeMapSignalKind.Control),
			new("iwatchlist", "watch calls", "session", "watch calls", TradeMapSignalKind.Control),
			new("session", "watchlist state", "iwatchlist", "watchlist state", TradeMapSignalKind.Display),
			new("iwatchlist", "watchlist state", "watchlist", "watchlist state", TradeMapSignalKind.Display),
			new("chart", "tick history calls", "ihistory", "tick history calls", TradeMapSignalKind.Control),
			new("ihistory", "tick history calls", "session", "tick history calls", TradeMapSignalKind.Control),
			new("session", "tick stream", "itickstream", "tick stream", TradeMapSignalKind.Display),
			new("itickstream", "tick stream", "chart", "tick stream", TradeMapSignalKind.Display),
			new("itickstream", "tick stream", "financials", "tick stream", TradeMapSignalKind.Display),
			new("itickstream", "tick stream", "controls", "tick stream", TradeMapSignalKind.Display),
			new("controls", "order calls", "itradeexec", "order calls", TradeMapSignalKind.Control),
			new("itradeexec", "order calls", "session", "order calls", TradeMapSignalKind.Control),
			new("financials", "position close calls", "itradeexec", "position close calls", TradeMapSignalKind.Control),
			new("itradeexec", "position close calls", "session", "position close calls", TradeMapSignalKind.Control),
			new("session", "connection state", "iaccount", "connection state", TradeMapSignalKind.Display),
			new("iaccount", "connection state", "tradecoordinator", "connection state", TradeMapSignalKind.Display),
			new(
				"tradecoordinator",
				"session lifecycle calls",
				"iaccount",
				"session lifecycle calls",
				TradeMapSignalKind.Control
			),
			new(
				"iaccount",
				"session lifecycle calls",
				"session",
				"session lifecycle calls",
				TradeMapSignalKind.Control
			),
			new("watchlist", "category calls", "icategory", "category calls", TradeMapSignalKind.Control),
			new("icategory", "category calls", "symsvc", "category calls", TradeMapSignalKind.Control),
			new("watchlist", "ensure metadata", "imetaensure", "ensure metadata", TradeMapSignalKind.Control),
			new("imetaensure", "ensure metadata", "metacache", "ensure metadata", TradeMapSignalKind.Control),
			new("metacache", "metadata snapshot", "imetasnapshot", "metadata snapshot", TradeMapSignalKind.Display),
			new("imetasnapshot", "metadata snapshot", "financials", "metadata snapshot", TradeMapSignalKind.Display),
			new("imetasnapshot", "metadata snapshot", "controls", "metadata snapshot", TradeMapSignalKind.Display),
			// TradeCoordinator keeps only what has no driver owner: cross-panel UI state.
			new("watchlist", "symbol selected", "tradecoordinator", "symbol selected", TradeMapSignalKind.Control),
			new("watchlist", "symbol unwatched", "tradecoordinator", "symbol unwatched", TradeMapSignalKind.Control),
			new("watchlist", "sidebar collapsed", "tradecoordinator", "sidebar collapsed", TradeMapSignalKind.Control),
			new("tradecoordinator", "sidebar collapsed", "watchlist", "sidebar collapsed", TradeMapSignalKind.Display),
			new("tradecoordinator", "selected symbol", "watchlist", "selected symbol", TradeMapSignalKind.Display),
			new("tradecoordinator", "chart parameters", "chart", "chart parameters", TradeMapSignalKind.Display),
			new(
				"chart",
				"chart settings snapshot",
				"tradecoordinator",
				"chart settings snapshot",
				TradeMapSignalKind.Control
			),
			new("financials", "position selected", "tradecoordinator", "position selected", TradeMapSignalKind.Control),
			new("tradecoordinator", "selected symbol", "controls", "selected symbol", TradeMapSignalKind.Display),
			new("financials", "position rows", "positiontable", "position rows", TradeMapSignalKind.Display),
			new("positiontable", "row selected", "financials", "row selected", TradeMapSignalKind.Control),
			new("positiontable", "row closed", "financials", "row closed", TradeMapSignalKind.Control),
			new("userspace-watchlist", "user input", "watchlist", "user input", TradeMapSignalKind.Control),
			new("userspace-chart", "user input", "chart", "user input", TradeMapSignalKind.Control),
			new("userspace-positiontable", "user input", "positiontable", "user input", TradeMapSignalKind.Control),
			new("userspace-controls", "user input", "controls", "user input", TradeMapSignalKind.Control),
		};
	}
}

