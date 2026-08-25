using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// Data/pixels split: every row/column/cell value arrives pre-computed via Grid (see XGridStats.Compute);
	/// this component turns the active display state into a flat numeric payload and hands it to xGrid.js,
	/// which paints the footprint onto one canvas instead of one DOM node per cell. Only the grid
	/// cells/overlays/markers moved to canvas -- the header, toggles, totals and Net bar chart stay declarative markup.
	/// </summary>
	public partial class XGrid : ComponentBase, IAsyncDisposable
	{
		[Inject]
		private IJSRuntime JSRuntime { get; set; } = null!;

		[Parameter]
		public XGridResult? Grid { get; set; }

		/// <summary>
		/// Two-way bound so the host (the only one that can call XGridStats.Compute) recomputes Grid when
		/// either changes. PipCount x the instrument's pip size is the fixed row height the host passes
		/// into XGridStats.Compute -- row count is then derived from however many bands the price range needs.
		/// </summary>
		[Parameter]
		public int PipCount { get; set; } = 20;

		[Parameter]
		public EventCallback<int> PipCountChanged { get; set; }

		[Parameter]
		public int ColCount { get; set; } = 20;

		[Parameter]
		public EventCallback<int> ColCountChanged { get; set; }

		/// <summary>
		/// Host-supplied "Profile Bars" window size (bars back from now driving the host's volume
		/// profile) -- null when the host has no such concept. Only then does the Cols control offer
		/// a button to snap ColCount onto it.
		/// </summary>
		[Parameter]
		public int? ProfileBarCount { get; set; }

		private Task SetPipCount(int value) => PipCountChanged.InvokeAsync(PipCount = value);

		private Task SetColCount(int value) => ColCountChanged.InvokeAsync(ColCount = value);

		// While bound, Cols tracks ProfileBarCount on every parameter set (OnParametersSetAsync
		// below) instead of being independently editable.
		private bool _colsBoundToProfileBars;

		private Task ToggleColsBoundToProfileBars()
		{
			_colsBoundToProfileBars = !_colsBoundToProfileBars;
			return _colsBoundToProfileBars ? SyncColsToProfileBars() : Task.CompletedTask;
		}

		private Task SyncColsToProfileBars() =>
			ProfileBarCount is { } count && count != ColCount ? SetColCount(count) : Task.CompletedTask;

		protected override Task OnParametersSetAsync() =>
			_colsBoundToProfileBars ? SyncColsToProfileBars() : Task.CompletedTask;

		// Local overlay state, purely visual, so it stays unbound rather than a two-way parameter.
		private bool _expanded;

		private void ToggleExpanded() => _expanded = !_expanded;

		/// <summary>
		/// Host-provided controls shown in a top bar only while expanded, letting the hosting page drop
		/// mini playback/profile controls beside the fullscreen grid.
		/// </summary>
		[Parameter]
		public RenderFragment? ExpandedControls { get; set; }

		/// <summary>
		/// Lines a host process computes and pins over the grid -- rendered alongside the user's own
		/// drawings but owned by the host, so the user's Clear leaves them intact.
		/// </summary>
		[Parameter]
		public IReadOnlyList<XGridLine>? Lines { get; set; }

		/// <summary>
		/// Raised when the user completes a drawing. The imperative counterpart is AddLine/ClearLines
		/// for hosts holding an @ref.
		/// </summary>
		[Parameter]
		public EventCallback<XGridLine> OnLineDrawn { get; set; }

		/// <summary>
		/// Trades a host process pins over the grid -- open/close markers with a connecting line, plus a
		/// right-edge direction marker per open trade coloured by live P&amp;L. Host-owned and recomputed
		/// per render, so live price and profit/loss stay current.
		/// </summary>
		[Parameter]
		public IReadOnlyList<XGridPosition>? Positions { get; set; }

		/// <summary>
		/// Raised when the user presses Place on the right-margin order panel -- the host maps the
		/// absolute prices onto its own order model and executes it.
		/// </summary>
		[Parameter]
		public EventCallback<XGridOrder> OnOrderPlaced { get; set; }

		/// <summary>
		/// Resting orders a host pins at their trigger price -- dotted level plus a right-edge
		/// direction marker, recomputed per render like Positions.
		/// </summary>
		[Parameter]
		public IReadOnlyList<XGridPending>? Pending { get; set; }

		/// <summary>
		/// Raised when the user applies edits to a resting order -- the host cancels the old one and
		/// re-places it, since the engine has no in-place modify.
		/// </summary>
		[Parameter]
		public EventCallback<XGridOrderModification> OnPendingModified { get; set; }

		/// <summary>Raised with a pending order's Id when the user cancels it.</summary>
		[Parameter]
		public EventCallback<string> OnPendingCancelled { get; set; }

		/// <summary>Raised with an open position's Id when the user closes it.</summary>
		[Parameter]
		public EventCallback<string> OnPositionClosed { get; set; }

		// Working values behind the right-margin panel -- entry price from the clicked cell,
		// optional SL/TP pinned by a follow-up click while armed. Null when the panel is shut.
		private sealed class OrderDraft
		{
			public double EntryPrice { get; set; }
			public double Volume { get; set; } = 1;
			public bool IsLong { get; set; } = true;
			public double? StopLoss { get; set; }
			public double? TakeProfit { get; set; }
		}

		// New drafts a fresh order; Pending/Open bind the panel to an existing order, addressed by _panelTargetId.
		private enum OrderPanelMode
		{
			New,
			Pending,
			Open,
		}

		private enum OrderArm
		{
			None,
			StopLoss,
			TakeProfit,
		}

		private OrderDraft? _order;
		private OrderArm _orderArm;
		private OrderPanelMode _panelMode = OrderPanelMode.New;
		private string? _panelTargetId;

		private void ToggleOrderDirection()
		{
			if (_panelMode == OrderPanelMode.New && _order is { } order)
				order.IsLong = !order.IsLong;
		}

		private void ArmStopLoss() => _orderArm = _orderArm == OrderArm.StopLoss ? OrderArm.None : OrderArm.StopLoss;

		private void ArmTakeProfit() =>
			_orderArm = _orderArm == OrderArm.TakeProfit ? OrderArm.None : OrderArm.TakeProfit;

		private void ClearStopLoss()
		{
			if (_order is { } order)
				order.StopLoss = null;
			if (_orderArm == OrderArm.StopLoss)
				_orderArm = OrderArm.None;
		}

		private void ClearTakeProfit()
		{
			if (_order is { } order)
				order.TakeProfit = null;
			if (_orderArm == OrderArm.TakeProfit)
				_orderArm = OrderArm.None;
		}

		private void CancelOrder()
		{
			_order = null;
			_orderArm = OrderArm.None;
			_panelMode = OrderPanelMode.New;
			_panelTargetId = null;
		}

		private async Task PlaceOrder()
		{
			if (_order is not { } order)
				return;
			await OnOrderPlaced.InvokeAsync(
				new XGridOrder(order.IsLong, order.EntryPrice, order.Volume, order.StopLoss, order.TakeProfit)
			);
			CancelOrder();
		}

		private async Task ApplyModification()
		{
			if (_order is not { } order || _panelTargetId is not { } id)
				return;
			await OnPendingModified.InvokeAsync(
				new XGridOrderModification(
					id,
					order.IsLong,
					order.EntryPrice,
					order.Volume,
					order.StopLoss,
					order.TakeProfit
				)
			);
			CancelOrder();
		}

		private async Task CancelPending()
		{
			if (_panelTargetId is { } id)
				await OnPendingCancelled.InvokeAsync(id);
			CancelOrder();
		}

		private async Task ClosePosition()
		{
			if (_panelTargetId is { } id)
				await OnPositionClosed.InvokeAsync(id);
			CancelOrder();
		}

		private string PanelTag =>
			_panelMode switch
			{
				OrderPanelMode.Pending => "PENDING",
				OrderPanelMode.Open => "OPEN",
				_ => "NEW",
			};

		private double OrderPanelTopPercent(XGridResult grid)
		{
			if (_order is not { } order)
				return 0;
			int rows = grid.RowLow.Count;
			return rows == 0 ? 0 : PriceToRowF(grid, order.EntryPrice) / (rows + 1) * 100;
		}

		private readonly List<XGridLine> _userLines = new();

		// Opt-in segment tool -- while active a click places a start cell, the next an end cell.
		private bool _lineMode;

		private void ToggleLineMode() => _lineMode = !_lineMode;

		// Purely a display choice over data already on Grid, so it stays local, unbound state.
		private enum SweepMode
		{
			None,
			LeftToRight,
			RightToLeft,
			Sum,
		}

		private SweepMode _sweepMode = SweepMode.None;

		// Opt-in delta shading -- recolours each cell by its up-minus-down tick balance
		// instead of its raw tick count, same display-choice rationale as _sweepMode.
		private bool _shadeDelta;

		// Weights each tick by the price distance it moved instead of counting it as one --
		// modifies whichever shading is active (count or delta).
		private bool _distanceWeighted;

		// Opt-in line graph of NetByColumn under the totals row -- default off like every other overlay.
		private bool _showNetGraph;

		// Per-metric UI state for one Spectrum: ShowGraph opts into its distribution graph, ShowHighlight
		// opts into bordering the top Percent% of its cells on the main grid, grouped per metric.
		private sealed class SpectrumOverlay
		{
			public bool ShowGraph;
			public bool ShowHighlight;
			public double Percent = 10;
		}

		private readonly SpectrumOverlay _spectrum = new();
		private readonly SpectrumOverlay _upSpectrum = new();
		private readonly SpectrumOverlay _downSpectrum = new();

		private SpectrumOverlay OverlayFor(XGridSpectrumMetric metric) =>
			metric switch
			{
				XGridSpectrumMetric.Up => _upSpectrum,
				XGridSpectrumMetric.Down => _downSpectrum,
				_ => _spectrum,
			};

		// Reuses the same percent already configured for that metric's Highlight/Spectrum overlay,
		// so the signal panel agrees with whatever cutoff is drawn on screen.
		private bool CurrentSignal(XGridResult grid, XGridSpectrumMetric metric) =>
			XGridStats.IsCurrentInTopPercent(grid, metric, OverlayFor(metric).Percent);

		// Poc/Vah/Val/Vwap/Average/Median are independently toggleable, every overlay starting off.
		private bool _showPoc;
		private bool _showVah;
		private bool _showVal;
		private bool _showVwap;
		private bool _showAverage;
		private bool _showMedian;

		private IJSObjectReference? _module;
		private DotNetObjectReference<XGrid>? _dotNetRef;
		private ElementReference _canvasRef;
		private bool _disposed;

		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			try
			{
				if (_module is null)
				{
					_module = await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/xGrid.js");
					if (_disposed)
						return;
					_dotNetRef = DotNetObjectReference.Create(this);
				}

				// The canvas is only in the DOM when this same condition rendered it, so
				// _canvasRef is valid exactly then -- guarding here keeps draw off a stale ref.
				if (!_disposed && Grid is { } grid)
					await _module.InvokeVoidAsync("draw", _canvasRef, _dotNetRef, BuildPayload(grid));
			}
			catch (JSDisconnectedException) { }
			catch (ObjectDisposedException) { }
		}

		// One byte per cell (128 = zero, sign carried for delta mode), keeping the payload a byte[]
		// (base64 fast path over interop) instead of a colour string per cell.
		private object BuildPayload(XGridResult grid)
		{
			int rows = grid.RowLow.Count,
				cols = grid.Columns.Count;
			var cells = new byte[rows * cols];
			var present = new byte[rows * cols];
			for (var r = 0; r < rows; r++)
			for (var c = 0; c < cols; c++)
			{
				cells[r * cols + c] = EncodeIntensity(CellIntensity(grid, r, c));
				present[r * cols + c] = grid.Columns[c].Cells[r].Count > 0 ? (byte)1 : (byte)0;
			}

			double[]? sweep = null;
			if (_sweepMode != SweepMode.None)
			{
				sweep = new double[rows];
				for (var r = 0; r < rows; r++)
					sweep[r] = RowSweepIntensity(grid, r);
			}

			var markers = new List<object>();
			void Add(bool show, XGridLevel? level, string kind)
			{
				if (show && level is { } l)
					markers.Add(new { row = l.Row, kind });
			}
			Add(_showPoc, grid.Poc, "poc");
			Add(_showVah, grid.Vah, "vah");
			Add(_showVal, grid.Val, "val");
			Add(_showVwap, grid.Vwap, "vwap");
			Add(_showAverage, grid.Average, "avg");
			Add(_showMedian, grid.Median, "med");

			var lines = new List<object>();
			var toggledRows = new List<int>();
			var toggledCols = new List<int>();
			void Collect(XGridLine line, bool external, int index)
			{
				lines.Add(LinePayload(grid, line, external, index));
				if (line.Kind == XGridLineKind.Horizontal)
				{
					int r = PriceToRow(grid, line.StartPrice);
					if (r >= 0)
						toggledRows.Add(r);
				}
				else if (line.Kind == XGridLineKind.Vertical)
				{
					int c = TimeToCol(grid, line.StartTime);
					if (c >= 0)
						toggledCols.Add(c);
				}
			}
			if (Lines is not null)
				foreach (var line in Lines)
					Collect(line, external: true, index: -1);
			for (var i = 0; i < _userLines.Count; i++)
				Collect(_userLines[i], external: false, index: i);

			double? currentPrice = grid.Current is { } cur ? cur.Price : null;
			var positions = new List<object>();
			if (Positions is not null)
				foreach (var pos in Positions)
					positions.Add(PositionPayload(grid, pos, currentPrice));

			var pending = new List<object>();
			if (Pending is not null)
				foreach (var rest in Pending)
					pending.Add(
						new
						{
							y = PriceToRowF(grid, rest.TriggerPrice),
							dir = rest.IsLong ? "long" : "short",
							tip = $"Pending {(rest.IsLong ? "long" : "short")} @ {FormatPrice(rest.TriggerPrice)}",
						}
					);

			object? draft = null;
			if (_order is { } order && _panelMode != OrderPanelMode.Open)
			{
				var draftLines = new List<object> { new { y = PriceToRowF(grid, order.EntryPrice), kind = "entry" } };
				if (order.StopLoss is { } slPrice)
					draftLines.Add(new { y = PriceToRowF(grid, slPrice), kind = "sl" });
				if (order.TakeProfit is { } tpPrice)
					draftLines.Add(new { y = PriceToRowF(grid, tpPrice), kind = "tp" });
				draft = new { lines = draftLines };
			}

			return new
			{
				rows,
				cols,
				mode = _shadeDelta ? "delta" : "count",
				cells,
				present,
				topHighlight = TopHighlightMask(grid, XGridSpectrumMetric.Total),
				upHighlight = TopHighlightMask(grid, XGridSpectrumMetric.Up),
				downHighlight = TopHighlightMask(grid, XGridSpectrumMetric.Down),
				sweep,
				markers,
				current = grid.Current is { } cur2 ? (int?)cur2.Row : null,
				lines,
				toggledRows,
				toggledCols,
				positions,
				pending,
				draft,
				orderArm = _orderArm switch
				{
					OrderArm.StopLoss => "sl",
					OrderArm.TakeProfit => "tp",
					_ => "entry",
				},
				margins = true,
				lineMode = _lineMode,
			};
		}

		private static object PositionPayload(XGridResult grid, XGridPosition pos, double? currentPrice)
		{
			bool open = pos.CloseTime is null || pos.ClosePrice is null;
			double reference = open ? currentPrice ?? pos.OpenPrice : pos.ClosePrice!.Value;
			double direction = pos.Direction == XGridPositionDirection.Long ? 1 : -1;
			double favour = (reference - pos.OpenPrice) * direction;
			int state =
				favour > 0 ? 1
				: favour < 0 ? -1
				: 0;

			DateTime nowTime = grid.Columns.Count > 0 ? grid.Columns[^1].OpenTime : pos.OpenTime;
			DateTime closeTime = open ? nowTime : pos.CloseTime!.Value;
			double closePrice = open ? reference : pos.ClosePrice!.Value;

			return new
			{
				dir = pos.Direction == XGridPositionDirection.Long ? "long" : "short",
				ox = TimeToColF(grid, pos.OpenTime),
				oy = PriceToRowF(grid, pos.OpenPrice),
				cx = TimeToColF(grid, closeTime),
				cy = PriceToRowF(grid, closePrice),
				entryY = PriceToRowF(grid, pos.OpenPrice),
				slY = open && pos.StopLoss is { } slPrice ? (double?)PriceToRowF(grid, slPrice) : null,
				tpY = open && pos.TakeProfit is { } tpPrice ? (double?)PriceToRowF(grid, tpPrice) : null,
				open,
				state,
				tip = PositionTip(pos, reference, state),
			};
		}

		private static string PositionTip(XGridPosition pos, double reference, int state)
		{
			string dir = pos.Direction == XGridPositionDirection.Long ? "Long" : "Short";
			string pnl =
				state > 0 ? "in profit"
				: state < 0 ? "in loss"
				: "flat";
			string close = pos.ClosePrice is { } cp
				? $" -> {FormatTime(pos.CloseTime!.Value)} @ {FormatPrice(cp)}"
				: $" (open, now {FormatPrice(reference)})";
			return $"{dir} @ {FormatTime(pos.OpenTime)} {FormatPrice(pos.OpenPrice)}{close} | {pnl}";
		}

		private static object LinePayload(XGridResult grid, XGridLine line, bool external, int index) =>
			new
			{
				kind = line.Kind switch
				{
					XGridLineKind.Vertical => "vertical",
					XGridLineKind.Horizontal => "horizontal",
					_ => "segment",
				},
				x1 = TimeToColF(grid, line.StartTime),
				y1 = PriceToRowF(grid, line.StartPrice),
				x2 = TimeToColF(grid, line.EndTime),
				y2 = PriceToRowF(grid, line.EndPrice),
				external,
				index,
				tip = LineTip(line),
			};

		// Signed -1..1 (delta) or 0..1 (count), same choice of metric and ceiling the CSS-grid
		// fill used -- distance weighting swaps each tick's weight of one for its moved price.
		private double CellIntensity(XGridResult grid, int row, int col)
		{
			XGridCell cell = grid.Columns[col].Cells[row];
			if (_shadeDelta)
			{
				double delta = _distanceWeighted ? cell.UpDistance - cell.DownDistance : cell.UpCount - cell.DownCount;
				double ceiling = _distanceWeighted ? grid.MaxAbsDeltaDistance : grid.MaxAbsDelta;
				return ceiling <= 0 ? 0 : delta / ceiling;
			}

			double value = _distanceWeighted ? cell.UpDistance + cell.DownDistance : cell.Value;
			double maxValue = _distanceWeighted ? grid.MaxDistanceValue : grid.MaxValue;
			return maxValue <= 0 ? 0 : value / maxValue;
		}

		private static byte EncodeIntensity(double v) => (byte)Math.Clamp((int)Math.Round(128 + v * 127), 0, 255);

		[JSInvokable]
		public string CellTooltip(int row, int col)
		{
			if (Grid is not { } grid)
				return "";
			if (row < 0 || col < 0 || row >= grid.RowLow.Count || col >= grid.Columns.Count)
				return "";

			XGridCell cell = grid.Columns[col].Cells[row];
			int deltaUpDown = cell.UpCount - cell.DownCount;
			var lines = new List<string>
			{
				$"{grid.RowLow[row]:F5} - {grid.RowHigh[row]:F5} @ {grid.Columns[col].OpenTime:HH:mm}",
				$"Ticks {cell.Count}  Up {cell.UpCount}  Down {cell.DownCount}  Δ {deltaUpDown:+0;-0;0}",
			};

			if (_sweepMode != SweepMode.None)
				lines.Add($"POC on {RowSweepValue(grid, row):0} sweep steps");

			var levels = new List<string>();
			void Note(bool show, XGridLevel? level, string label)
			{
				if (show && level is { } l && l.Row == row)
					levels.Add(label);
			}
			Note(_showPoc, grid.Poc, "POC");
			Note(_showVah, grid.Vah, "VAH");
			Note(_showVal, grid.Val, "VAL");
			Note(_showVwap, grid.Vwap, "VWAP");
			Note(_showAverage, grid.Average, "AVG");
			Note(_showMedian, grid.Median, "MED");
			if (grid.Current is { } current && current.Row == row)
				levels.Add("current");
			if (levels.Count > 0)
				lines.Add(string.Join(", ", levels));

			return string.Join('\n', lines);
		}

		[JSInvokable]
		public async Task RightMarginClick(int row)
		{
			if (Grid is not { } grid || !InRow(grid, row))
				return;
			double price = RowMid(grid, row);

			if (_orderArm == OrderArm.StopLoss && _order is { } sl)
			{
				sl.StopLoss = price;
				_orderArm = OrderArm.None;
			}
			else if (_orderArm == OrderArm.TakeProfit && _order is { } tp)
			{
				tp.TakeProfit = price;
				_orderArm = OrderArm.None;
			}
			else if (FindPendingAt(grid, row) is { } pendingOrder)
			{
				OpenPendingPanel(pendingOrder);
			}
			else if (FindOpenPositionAt(grid, row) is { } position)
			{
				OpenPositionPanel(position);
			}
			else
			{
				_panelMode = OrderPanelMode.New;
				_panelTargetId = null;
				_order ??= new OrderDraft();
				_order.EntryPrice = price;
			}
			await InvokeAsync(StateHasChanged);
		}

		private XGridPending? FindPendingAt(XGridResult grid, int row)
		{
			if (Pending is null)
				return null;
			foreach (var rest in Pending)
				if (PriceToRow(grid, rest.TriggerPrice) == row)
					return rest;
			return null;
		}

		private XGridPosition? FindOpenPositionAt(XGridResult grid, int row)
		{
			if (Positions is null)
				return null;
			foreach (var pos in Positions)
				if (pos.CloseTime is null && pos.ClosePrice is null && PriceToRow(grid, pos.OpenPrice) == row)
					return pos;
			return null;
		}

		private void OpenPendingPanel(XGridPending pendingOrder)
		{
			_panelMode = OrderPanelMode.Pending;
			_panelTargetId = pendingOrder.Id ?? "";
			_orderArm = OrderArm.None;
			_order = new OrderDraft
			{
				EntryPrice = pendingOrder.TriggerPrice,
				Volume = pendingOrder.Volume,
				IsLong = pendingOrder.IsLong,
				StopLoss = pendingOrder.StopLoss,
				TakeProfit = pendingOrder.TakeProfit,
			};
		}

		private void OpenPositionPanel(XGridPosition position)
		{
			_panelMode = OrderPanelMode.Open;
			_panelTargetId = position.Id ?? "";
			_orderArm = OrderArm.None;
			_order = new OrderDraft
			{
				EntryPrice = position.OpenPrice,
				IsLong = position.Direction == XGridPositionDirection.Long,
				StopLoss = position.StopLoss,
				TakeProfit = position.TakeProfit,
			};
		}

		[JSInvokable]
		public async Task AddUserLine(int startRow, int startCol, int endRow, int endCol)
		{
			if (Grid is not { } grid)
				return;
			if (!InRow(grid, startRow) || !InCol(grid, startCol) || !InRow(grid, endRow) || !InCol(grid, endCol))
				return;
			var line = XGridLine.Segment(
				grid.Columns[startCol].OpenTime,
				RowMid(grid, startRow),
				grid.Columns[endCol].OpenTime,
				RowMid(grid, endRow)
			);
			_userLines.Add(line);
			await OnLineDrawn.InvokeAsync(line);
			await InvokeAsync(StateHasChanged);
		}

		[JSInvokable]
		public Task ToggleVerticalLine(int col)
		{
			if (Grid is not { } grid || !InCol(grid, col))
				return Task.CompletedTask;
			return ToggleLine(
				l => l.Kind == XGridLineKind.Vertical && TimeToCol(grid, l.StartTime) == col,
				() => XGridLine.Vertical(grid.Columns[col].OpenTime)
			);
		}

		[JSInvokable]
		public Task ToggleHorizontalLine(int row)
		{
			if (Grid is not { } grid || !InRow(grid, row))
				return Task.CompletedTask;
			return ToggleLine(
				l => l.Kind == XGridLineKind.Horizontal && PriceToRow(grid, l.StartPrice) == row,
				() => XGridLine.Horizontal(RowMid(grid, row))
			);
		}

		[JSInvokable]
		public async Task RemoveUserLine(int index)
		{
			if (index < 0 || index >= _userLines.Count)
				return;
			_userLines.RemoveAt(index);
			await InvokeAsync(StateHasChanged);
		}

		public void AddLine(XGridLine line)
		{
			_userLines.Add(line);
			StateHasChanged();
		}

		public void ClearLines()
		{
			_userLines.Clear();
			StateHasChanged();
		}

		private async Task ToggleLine(Predicate<XGridLine> match, Func<XGridLine> create)
		{
			int existing = _userLines.FindIndex(match);
			if (existing >= 0)
			{
				_userLines.RemoveAt(existing);
			}
			else
			{
				var line = create();
				_userLines.Add(line);
				await OnLineDrawn.InvokeAsync(line);
			}
			await InvokeAsync(StateHasChanged);
		}

		private static bool InRow(XGridResult grid, int row) => row >= 0 && row < grid.RowLow.Count;

		private static bool InCol(XGridResult grid, int col) => col >= 0 && col < grid.Columns.Count;

		private static double RowMid(XGridResult grid, int row) => (grid.RowLow[row] + grid.RowHigh[row]) / 2;

		private static string LineTip(XGridLine line) =>
			line.Kind switch
			{
				XGridLineKind.Vertical => $"Vertical @ {FormatTime(line.StartTime)}",
				XGridLineKind.Horizontal => $"Horizontal @ {FormatPrice(line.StartPrice)}",
				_ => $"Line: {FormatTime(line.StartTime)} @ {FormatPrice(line.StartPrice)} "
					+ $"-> {FormatTime(line.EndTime)} @ {FormatPrice(line.EndPrice)}",
			};

		private static string FormatTime(DateTime time) =>
			time.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

		private static string FormatPrice(double price) => price.ToString("F5", CultureInfo.InvariantCulture);

		// Rows run high-to-low; the top edge is RowHigh[0], the bottom edge RowLow[last]. The
		// fractional form below returns the continuous position so a re-mapped line lands exactly on its price.
		private static int PriceToRow(XGridResult grid, double price)
		{
			int rows = grid.RowHigh.Count;
			if (rows == 0)
				return -1;
			if (price >= grid.RowHigh[0])
				return 0;
			if (price <= grid.RowLow[rows - 1])
				return rows - 1;
			for (var r = 0; r < rows; r++)
				if (price <= grid.RowHigh[r] && price >= grid.RowLow[r])
					return r;
			return -1;
		}

		private static double PriceToRowF(XGridResult grid, double price)
		{
			int rows = grid.RowHigh.Count;
			if (rows == 0)
				return 0;
			if (price >= grid.RowHigh[0])
				return 0;
			if (price <= grid.RowLow[rows - 1])
				return rows;
			for (var r = 0; r < rows; r++)
			{
				if (price <= grid.RowHigh[r] && price >= grid.RowLow[r])
				{
					double span = grid.RowHigh[r] - grid.RowLow[r];
					return r + (span <= 0 ? 0.5 : (grid.RowHigh[r] - price) / span);
				}
			}
			return rows / 2.0;
		}

		// Columns run left-to-right by OpenTime; the fractional form below interpolates between
		// adjacent open times so a re-mapped line tracks time across a shifting range.
		private static int TimeToCol(XGridResult grid, DateTime time)
		{
			int cols = grid.Columns.Count;
			if (cols == 0)
				return -1;
			if (time <= grid.Columns[0].OpenTime)
				return 0;
			if (time >= grid.Columns[cols - 1].OpenTime)
				return cols - 1;
			for (var c = 0; c < cols - 1; c++)
			{
				DateTime t0 = grid.Columns[c].OpenTime,
					t1 = grid.Columns[c + 1].OpenTime;
				if (time >= t0 && time < t1)
					return (time - t0) <= (t1 - time) ? c : c + 1;
			}
			return cols - 1;
		}

		private static double TimeToColF(XGridResult grid, DateTime time)
		{
			int cols = grid.Columns.Count;
			if (cols == 0)
				return 0;
			if (time <= grid.Columns[0].OpenTime)
				return 0.5;
			if (time >= grid.Columns[cols - 1].OpenTime)
				return cols - 0.5;
			for (var c = 0; c < cols - 1; c++)
			{
				DateTime t0 = grid.Columns[c].OpenTime,
					t1 = grid.Columns[c + 1].OpenTime;
				if (time >= t0 && time < t1)
				{
					double span = (t1 - t0).TotalSeconds;
					return (c + 0.5) + (span <= 0 ? 0 : (time - t0).TotalSeconds / span);
				}
			}
			return cols - 0.5;
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
			_dotNetRef?.Dispose();
			GC.SuppressFinalize(this);
		}

		private readonly record struct NetBar(string X, string Y, string Width, string Height, bool Positive);

		// One bar per column, drawn from the zero baseline: positive Net rises above it, negative
		// drops below, mapped onto the same 0..100 viewBox the baseline/crossings use.
		private static IReadOnlyList<NetBar> NetGraphBars(XGridResult grid)
		{
			var series = grid.NetByColumn;
			var bars = new List<NetBar>(series.Count);
			if (series.Count == 0)
				return bars;

			var (min, range) = NetGraphSpan(series);
			double zeroY = range <= 0 ? 50 : 100 - (0 - min) / range * 100;
			double slot = 100.0 / series.Count;
			double gap = slot * 0.15;
			double width = slot - gap;
			for (var i = 0; i < series.Count; i++)
			{
				double value = series[i];
				double valueY = range <= 0 ? zeroY : 100 - (value - min) / range * 100;
				double top = Math.Min(zeroY, valueY);
				bars.Add(
					new NetBar(
						XForm(i * slot + gap / 2),
						XForm(top),
						XForm(width),
						XForm(Math.Abs(zeroY - valueY)),
						value >= 0
					)
				);
			}
			return bars;
		}

		private static string NetGraphZeroY(XGridResult grid)
		{
			var (min, range) = NetGraphSpan(grid.NetByColumn);
			double y = range <= 0 ? 50 : 100 - (0 - min) / range * 100;
			return y.ToString("0.##", CultureInfo.InvariantCulture);
		}

		// Every x (0..100 viewBox) where the cumulative Net line touches or crosses zero: an exact
		// zero point contributes its own x, a sign change contributes the interpolated crossing.
		private static IReadOnlyList<string> NetGraphZeroCrossings(XGridResult grid)
		{
			var series = grid.NetByColumn;
			var crossings = new List<string>();
			if (series.Count < 2)
				return crossings;

			for (var i = 1; i < series.Count; i++)
			{
				double a = series[i - 1],
					b = series[i];
				double xa = (double)(i - 1) / (series.Count - 1) * 100;
				double xb = (double)i / (series.Count - 1) * 100;
				if (a == 0)
					crossings.Add(XForm(xa));
				if ((a < 0 && b > 0) || (a > 0 && b < 0))
					crossings.Add(XForm(xa + a / (a - b) * (xb - xa)));
			}
			if (series[^1] == 0)
				crossings.Add(XForm(100));
			return crossings;
		}

		private static string XForm(double x) => x.ToString("0.##", CultureInfo.InvariantCulture);

		// Rank i's x sits at i / (n - 1), its y at the cell's value as a fraction of the largest --
		// an ascending curve from bottom-left (smallest cell) to top-right (largest cell).
		private static string SpectrumCurvePoints(XGridResult grid, XGridSpectrumMetric metric)
		{
			var order = XGridStats.CellOrder(grid, metric);
			int n = order.Length;
			if (n == 0)
				return "";

			double max = XGridStats.CellValue(grid, order[^1], metric);
			var points = new string[n];
			for (var i = 0; i < n; i++)
			{
				double x = n <= 1 ? 0 : i * 100.0 / (n - 1);
				double y = max <= 0 ? 100 : 100 - XGridStats.CellValue(grid, order[i], metric) / max * 100;
				points[i] = $"{XForm(x)},{XForm(y)}";
			}
			return string.Join(' ', points);
		}

		// Closes SpectrumCurvePoints down to the baseline so it can fill as a solid area.
		private static string SpectrumAreaPoints(XGridResult grid, XGridSpectrumMetric metric)
		{
			var curve = SpectrumCurvePoints(grid, metric);
			return curve.Length == 0 ? "" : $"{curve} 100,100 0,100";
		}

		// The curve is monotonically ascending, so x alone (the cell's rank) locates it; null
		// when there's no current cell, same as every other level marker on this grid.
		private static string? SpectrumMarkerX(XGridResult grid, XGridSpectrumMetric metric)
		{
			if (XGridStats.CurrentCell(grid) is not { } cell)
				return null;
			var order = XGridStats.CellOrder(grid, metric);
			int rank = Array.IndexOf(order, cell);
			if (rank < 0)
				return null;

			int n = order.Length;
			return XForm(n <= 1 ? 0 : rank * 100.0 / (n - 1));
		}

		// One byte per grid cell, 1 for every cell within the top Percent% by this metric, fed to
		// xGrid.js to stroke a border around each. Null while that metric's overlay is off.
		private byte[]? TopHighlightMask(XGridResult grid, XGridSpectrumMetric metric)
		{
			var overlay = OverlayFor(metric);
			if (!overlay.ShowHighlight)
				return null;
			int rows = grid.RowLow.Count,
				cols = grid.Columns.Count;
			var mask = new byte[rows * cols];
			var order = XGridStats.CellOrder(grid, metric);
			int n = order.Length;
			int count = XGridStats.TopCount(n, overlay.Percent);
			for (var i = n - count; i < n; i++)
			{
				var (row, col) = order[i];
				mask[row * cols + col] = 1;
			}
			return mask;
		}

		// Null while that metric's overlay is off, or when the cutoff isn't a meaningful split
		// (no cells, or 0%/100% selects none/all of them).
		private string? SpectrumThresholdX(XGridResult grid, XGridSpectrumMetric metric)
		{
			var overlay = OverlayFor(metric);
			if (!overlay.ShowHighlight)
				return null;
			var order = XGridStats.CellOrder(grid, metric);
			int n = order.Length;
			if (n <= 1)
				return null;

			int count = XGridStats.TopCount(n, overlay.Percent);
			if (count <= 0 || count >= n)
				return null;
			return XForm((n - count) * 100.0 / (n - 1));
		}

		private static (double Min, double Range) NetGraphSpan(IReadOnlyList<double> series)
		{
			double min = 0,
				max = 0;
			foreach (var v in series)
			{
				if (v < min)
					min = v;
				if (v > max)
					max = v;
			}
			return (min, max - min);
		}

		// Row-uniform -- the sweep metric has no per-column dimension (see
		// XGridStats.ComputeSweeps), so the whole row shades the same regardless of column.
		private double RowSweepValue(XGridResult grid, int row) =>
			_sweepMode switch
			{
				SweepMode.LeftToRight => grid.SweepLeftToRight[row],
				SweepMode.RightToLeft => grid.SweepRightToLeft[row],
				SweepMode.Sum => grid.SweepSum[row],
				_ => 0,
			};

		private double RowSweepIntensity(XGridResult grid, int row) =>
			grid.MaxSweepValue <= 0 ? 0 : RowSweepValue(grid, row) / grid.MaxSweepValue;

		private static int GridTickCount(XGridResult grid)
		{
			var total = 0;
			foreach (var column in grid.Columns)
			foreach (var cell in column.Cells)
				total += cell.Count;
			return total;
		}

		private static string GridTimeSpan(XGridResult grid)
		{
			if (grid.Columns.Count == 0)
				return "--";
			return FormatDuration(grid.Columns[^1].OpenTime - grid.Columns[0].OpenTime);
		}

		private static string FormatDuration(TimeSpan span)
		{
			if (span.TotalDays >= 1)
				return $"{(int)span.TotalDays}d {span.Hours}h";
			if (span.TotalHours >= 1)
				return $"{(int)span.TotalHours}h {span.Minutes}m";
			if (span.TotalMinutes >= 1)
				return $"{(int)span.TotalMinutes}m {span.Seconds}s";
			return $"{(int)span.TotalSeconds}s";
		}

		// All rows share one height by construction, so the first row's span stands for every row's.
		private static string RowHeightLabel(XGridResult grid) =>
			grid.RowLow.Count == 0
				? "--"
				: (grid.RowHigh[0] - grid.RowLow[0]).ToString("F5", CultureInfo.InvariantCulture);

		// Columns can each cover a different number of source bars, so this is the average spacing between column open times.
		private static string ColumnWidthLabel(XGridResult grid)
		{
			int cols = grid.Columns.Count;
			return cols < 2
				? "--"
				: FormatDuration((grid.Columns[^1].OpenTime - grid.Columns[0].OpenTime) / (cols - 1));
		}
	}
}
