using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Poink.Sim.Trader.Risk;

namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// Data/pixels split: every row/column/cell value arrives pre-computed via Grid (see XGridStats.Compute);
	/// this component turns the active display state into a flat numeric payload and hands it to xGrid.js,
	/// which paints the footprint onto one canvas instead of one DOM node per cell. Only the grid
	/// cells/overlays/markers moved to canvas -- the header, toggles and totals stay declarative markup.
	/// The pips/cols/overlay/highlight controls that used to live here now belong to MetricsControlPanel;
	/// Display/Spectrum/LowPointOverlay/HighPointOverlay arrive as read-only parameters from XGridController.
	/// </summary>
	public partial class XGrid : ComponentBase, IAsyncDisposable
	{
		[Inject]
		private IJSRuntime JSRuntime { get; set; } = null!;

		[Parameter]
		public XGridResult? Grid { get; set; }

		[Parameter]
		public XGridDisplaySettings Display { get; set; } = new();

		[Parameter]
		public XGridSpectrumSettings Spectrum { get; set; } = new();

		[Parameter]
		public XGridPeakOverlaySettings LowPointOverlay { get; set; } = new();

		[Parameter]
		public XGridPeakOverlaySettings HighPointOverlay { get; set; } = new();

		[Parameter]
		public int PipCount { get; set; } = 20;

		[Parameter]
		public EventCallback<int> PipCountChanged { get; set; }

		[Parameter]
		public int ColCount { get; set; } = 20;

		[Parameter]
		public EventCallback<int> ColCountChanged { get; set; }

		/// <summary>
		/// Host-supplied "Profile Bars" window size -- null when the host has no such concept. Only
		/// then does the column axis offer a button to snap ColCount onto it.
		/// </summary>
		[Parameter]
		public int? ProfileBarCount { get; set; }

		private const int MaxPipCount = 1000;
		private const int MaxColCount = 500;

		private Task SetPipCount(int value) => PipCountChanged.InvokeAsync(PipCount = Math.Clamp(value, 1, MaxPipCount));

		private readonly string _pipTicksId = $"xgrid-pip-ticks-{Guid.NewGuid():N}";

		private static readonly IReadOnlyList<int> PipTickPositions = XGridLogScaleHelper.OrderOfTenPositions(MaxPipCount);

		private int PipSliderPosition => XGridLogScaleHelper.ValueToPosition(PipCount, MaxPipCount);

		private Task SetPipSliderPosition(int position) => SetPipCount(XGridLogScaleHelper.PositionToValue(position, MaxPipCount));

		private Task StepPipCount(int delta) => SetPipCount(PipCount + delta);

		private Task SetColCount(int value) => ColCountChanged.InvokeAsync(ColCount = Math.Clamp(value, 1, MaxColCount));

		// While bound, Cols tracks ProfileBarCount on every parameter set instead of being independently editable.
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

		/// <summary>
		/// A host-owned draft preview, drawn the same way as the user's own click-built draft --
		/// only used while the user isn't actively building one of their own (see BuildPayload).
		/// </summary>
		[Parameter]
		public XGridExternalDraft? ExternalDraft { get; set; }

		/// <summary>Instrument pip size for a measured segment's pip-delta row -- 0.0001 (5-digit FX default) when a host doesn't have a real one to give.</summary>
		[Parameter]
		public double PipSize { get; set; } = 0.0001;

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

		private const int BottomMarginPx = 28;

		private string OrderPanelTop(XGridResult grid)
		{
			if (_order is not { } order)
				return "0px";
			int rows = grid.RowLow.Count;
			double fraction = rows == 0 ? 0 : PriceToRowF(grid, order.EntryPrice) / rows;
			return $"calc((100% - {BottomMarginPx}px) * {fraction.ToString("0.####", CultureInfo.InvariantCulture)})";
		}

		private readonly List<XGridLine> _userLines = new();


		// Opt-in segment tool -- while active a click places a start cell, the next an end cell.
		private bool _lineMode;

		private void ToggleLineMode()
		{
			_lineMode = !_lineMode;
			if (_lineMode)
				SetSelectMode(false);
		}

		// Click-to-measure mode: cells, the left margin and the bottom margin select instead of drawing.
		private bool _selectMode;
		private readonly List<XGridSelection> _selections = new();

		/// <summary>Raised with every selected cell, row or column in click order -- all of one kind -- whenever it changes.</summary>
		[Parameter]
		public EventCallback<IReadOnlyList<XGridSelection>> SelectionChanged { get; set; }

		private void ToggleSelectMode() => SetSelectMode(!_selectMode);

		private void SetSelectMode(bool on)
		{
			_selectMode = on;
			if (on)
				_lineMode = false;
			else
				ClearSelections();
		}

		private void ClearSelections()
		{
			if (_selections.Count == 0)
				return;
			_selections.Clear();
			PublishSelections();
		}

		private void PublishSelections() => _ = SelectionChanged.InvokeAsync([.. _selections]);

		/// <summary>Drops one selection by its position in click order; the imperative counterpart to clicking it again.</summary>
		public void RemoveSelectionAt(int index)
		{
			if (index < 0 || index >= _selections.Count)
				return;
			_selections.RemoveAt(index);
			PublishSelections();
			StateHasChanged();
		}

		// Selections stay one kind so a differential always compares like with like.
		private Task SelectToggle(XGridSelection selection)
		{
			if (_selections.Count > 0 && _selections[0].Kind != selection.Kind)
				_selections.Clear();
			if (!_selections.Remove(selection))
				_selections.Add(selection);
			PublishSelections();
			return InvokeAsync(StateHasChanged);
		}

		[JSInvokable]
		public Task SelectCell(int row, int col) => SelectToggle(new XGridSelection(XGridSelectionKind.Cell, row, col));

		[JSInvokable]
		public Task SelectRow(int row) => SelectToggle(new XGridSelection(XGridSelectionKind.Row, row, 0));

		[JSInvokable]
		public Task SelectColumn(int col) => SelectToggle(new XGridSelection(XGridSelectionKind.Column, 0, col));

		// A new resolution re-slices the grid, so an index selected before it would point at different data.
		private int _selectionPipCount, _selectionColCount;

		protected override void OnParametersSet()
		{
			if ((_selectionPipCount, _selectionColCount) != (PipCount, ColCount))
			{
				(_selectionPipCount, _selectionColCount) = (PipCount, ColCount);
				ClearSelections();
			}
		}

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
			if (Spectrum.HighlightsEnabled && Display.SweepMode != XGridSweepMode.None)
			{
				sweep = new double[rows];
				for (var r = 0; r < rows; r++)
					sweep[r] = RowSweepIntensity(grid, r);
			}

			var markers = new List<object>();
			void Add(bool show, XGridLevel? level, string kind)
			{
				if (Display.OverlaysEnabled && show && level is { } l)
					markers.Add(new { row = l.Row, kind });
			}
			Add(Display.ShowPoc, grid.Poc, "poc");
			Add(Display.ShowVah, grid.Vah, "vah");
			Add(Display.ShowVal, grid.Val, "val");
			Add(Display.ShowVwap, grid.Vwap, "vwap");
			Add(Display.ShowAverage, grid.Average, "avg");
			Add(Display.ShowMedian, grid.Median, "med");
			Add(Display.ShowRegressionLine, grid.RegressionLine, "reg");

			var dayLines = Display.OverlaysEnabled && Display.ShowDayLines ? IntervalBoundaryColumns(grid, 24) : null;
			var intervalLines =
				Display.OverlaysEnabled && Display.ShowIntervalLines
					? IntervalBoundaryColumns(grid, Display.IntervalHours)
					: null;

			static List<object> PointsPayload(IReadOnlyList<(int Row, int Col)> points)
			{
				var payload = new List<object>(points.Count);
				foreach (var pt in points)
					payload.Add(new { row = pt.Row, col = pt.Col });
				return payload;
			}
			var lowPoints = Spectrum.HighlightsEnabled && LowPointOverlay.ShowHighlight
				? PointsPayload(XGridStats.LowPointCells(grid, LowPointOverlay.Settings))
				: null;
			var highPoints = Spectrum.HighlightsEnabled && HighPointOverlay.ShowHighlight
				? PointsPayload(XGridStats.HighPointCells(grid, HighPointOverlay.Settings))
				: null;

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
			else if (ExternalDraft is { } ext)
			{
				var draftLines = new List<object> { new { y = PriceToRowF(grid, ext.EntryPrice), kind = "entry" } };
				if (ext.StopLoss is { } slPrice)
					draftLines.Add(new { y = PriceToRowF(grid, slPrice), kind = "sl" });
				if (ext.TakeProfit is { } tpPrice)
					draftLines.Add(new { y = PriceToRowF(grid, tpPrice), kind = "tp" });
				draft = new { lines = draftLines };
			}

			return new
			{
				rows,
				cols,
				mode = Spectrum.HighlightsEnabled && Display.ShadeDelta ? "delta" : "count",
				cells,
				present,
				topHighlight = TopHighlightMask(grid, XGridSpectrumMetric.Total),
				upHighlight = TopHighlightMask(grid, XGridSpectrumMetric.Up),
				downHighlight = TopHighlightMask(grid, XGridSpectrumMetric.Down),
				deltaPosHighlight = DeltaHighlightMask(grid, positive: true),
				deltaNegHighlight = DeltaHighlightMask(grid, positive: false),
				lowPoints,
				highPoints,
				sweep,
				markers,
				dayLines,
				intervalLines,
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
				selectMode = _selectMode,
				selections = _selections.Select(sel => new { kind = sel.Kind.ToString().ToLowerInvariant(), row = sel.Row, col = sel.Col }).ToList(),
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

		private object LinePayload(XGridResult grid, XGridLine line, bool external, int index) =>
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
				tipRows = LineTipRows(line, PipSize),
			};

		// Signed -1..1 (delta) or 0..1 (count), same choice of metric and ceiling the CSS-grid
		// fill used -- distance weighting swaps each tick's weight of one for its moved price.
		private double CellIntensity(XGridResult grid, int row, int col)
		{
			XGridCell cell = grid.Columns[col].Cells[row];
			bool distanceWeighted = Spectrum.HighlightsEnabled && Display.DistanceWeighted;
			if (Spectrum.HighlightsEnabled && Display.ShadeDelta)
			{
				double delta = distanceWeighted ? cell.UpDistance - cell.DownDistance : cell.UpCount - cell.DownCount;
				double ceiling = distanceWeighted ? grid.MaxAbsDeltaDistance : grid.MaxAbsDelta;
				return ceiling <= 0 ? 0 : delta / ceiling;
			}

			double value = distanceWeighted ? cell.UpDistance + cell.DownDistance : cell.Value;
			double maxValue = distanceWeighted ? grid.MaxDistanceValue : grid.MaxValue;
			return maxValue <= 0 ? 0 : value / maxValue;
		}

		private static byte EncodeIntensity(double v) => (byte)Math.Clamp((int)Math.Round(128 + v * 127), 0, 255);

		[JSInvokable]
		public List<XGridTooltipRow> CellTooltip(int row, int col)
		{
			var rows = new List<XGridTooltipRow>();
			if (Grid is not { } grid)
				return rows;
			if (row < 0 || col < 0 || row >= grid.RowLow.Count || col >= grid.Columns.Count)
				return rows;

			XGridCell cell = grid.Columns[col].Cells[row];
			int deltaUpDown = cell.UpCount - cell.DownCount;
			rows.Add(new XGridTooltipRow("Price: ", $"{grid.RowLow[row]:F5} - {grid.RowHigh[row]:F5}"));
			rows.Add(new XGridTooltipRow("Time: ", $"{grid.Columns[col].OpenTime:HH:mm}"));
			rows.Add(new XGridTooltipRow("Ticks: ", $"{cell.Count}"));
			rows.Add(new XGridTooltipRow("Up: ", $"{cell.UpCount}"));
			rows.Add(new XGridTooltipRow("Down: ", $"{cell.DownCount}"));
			rows.Add(new XGridTooltipRow("Delta: ", $"{deltaUpDown:+0;-0;0}"));

			if (Display.SweepMode != XGridSweepMode.None)
				rows.Add(new XGridTooltipRow("Sweep: ", $"{RowSweepValue(grid, row):0} steps"));

			var levels = new List<string>();
			void Note(bool show, XGridLevel? level, string label)
			{
				if (show && level is { } l && l.Row == row)
					levels.Add(label);
			}
			Note(Display.ShowPoc, grid.Poc, "POC");
			Note(Display.ShowVah, grid.Vah, "VAH");
			Note(Display.ShowVal, grid.Val, "VAL");
			Note(Display.ShowVwap, grid.Vwap, "VWAP");
			Note(Display.ShowAverage, grid.Average, "AVG");
			Note(Display.ShowMedian, grid.Median, "MED");
			Note(Display.ShowRegressionLine, grid.RegressionLine, "REG");
			if (grid.Current is { } current && current.Row == row)
				levels.Add("current");
			if (levels.Count > 0)
				rows.Add(new XGridTooltipRow("Levels: ", string.Join(", ", levels)));

			return rows;
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

		// Duration/pip-delta/%-diff only mean something for a two-point Segment -- Horizontal/Vertical
		// are single-value reference lines, so they keep one row instead of three fabricated ones.
		private static IReadOnlyList<XGridTooltipRow> LineTipRows(XGridLine line, double pipSize)
		{
			if (line.Kind == XGridLineKind.Vertical)
				return new[] { new XGridTooltipRow("Time: ", FormatTime(line.StartTime)) };
			if (line.Kind == XGridLineKind.Horizontal)
				return new[] { new XGridTooltipRow("Price: ", FormatPrice(line.StartPrice)) };

			double pipDelta = RiskHelper.PriceDistanceInPips(line.StartPrice, line.EndPrice, pipSize);
			double percentDiff = line.StartPrice == 0 ? 0 : (line.EndPrice - line.StartPrice) / line.StartPrice * 100;
			return new[]
			{
				new XGridTooltipRow("Duration: ", FormatDuration((line.EndTime - line.StartTime).Duration())),
				new XGridTooltipRow("Pip Δ: ", pipDelta.ToString("F1", CultureInfo.InvariantCulture)),
				new XGridTooltipRow(
					"% Diff: ",
					$"{percentDiff.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture)}%",
					percentDiff switch
					{
						> 0 => "gain",
						< 0 => "loss",
						_ => null,
					}
				),
			};
		}

		private static string LinePillLabel(XGridLine line) =>
			line.Kind switch
			{
				XGridLineKind.Horizontal => $"H {FormatPrice(line.StartPrice)}",
				XGridLineKind.Vertical => $"V {line.StartTime:HH:mm}",
				_ => $"{FormatPrice(line.StartPrice)}→{FormatPrice(line.EndPrice)}",
			};

		private readonly record struct SegmentMeasure(string Duration, string PipDeltaText, string PercentText, bool Positive);

		// Signed (not RiskHelper's absolute PriceDistanceInPips) so the pill can colour by direction,
		// same green-up/red-down convention as SBuy/SSell and the price axis.
		private SegmentMeasure? MeasureSegment(XGridLine line)
		{
			if (line.Kind != XGridLineKind.Segment)
				return null;

			double signedPips = (line.EndPrice - line.StartPrice) / PipSize;
			double percent = line.StartPrice == 0 ? 0 : (line.EndPrice - line.StartPrice) / line.StartPrice * 100;
			return new SegmentMeasure(
				CompactDuration((line.EndTime - line.StartTime).Duration()),
				signedPips.ToString("+0;-0;0", CultureInfo.InvariantCulture),
				percent.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture),
				percent >= 0
			);
		}

		private static string CompactDuration(TimeSpan span) =>
			span.TotalDays >= 1 ? $"{(int)span.TotalDays}d"
			: span.TotalHours >= 1 ? $"{(int)span.TotalHours}h"
			: span.TotalMinutes >= 1 ? $"{(int)span.TotalMinutes}m"
			: $"{(int)span.TotalSeconds}s";

		private void RemoveLineAt(int index)
		{
			if (index < 0 || index >= _userLines.Count)
				return;
			_userLines.RemoveAt(index);
			StateHasChanged();
		}

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

		private (bool Show, double Percent) SpectrumOverlayFor(XGridSpectrumMetric metric) =>
			metric switch
			{
				XGridSpectrumMetric.Up => (Spectrum.UpShow, Spectrum.UpPercent),
				XGridSpectrumMetric.Down => (Spectrum.DownShow, Spectrum.DownPercent),
				_ => (Spectrum.TotalShow, Spectrum.TotalPercent),
			};

		// One byte per grid cell, 1 for every cell within the top Percent% by this metric (Rank:
		// share of nonzero cells; Max: share of the grid-wide peak), fed to xGrid.js for a border.
		private byte[]? TopHighlightMask(XGridResult grid, XGridSpectrumMetric metric)
		{
			var (show, percent) = SpectrumOverlayFor(metric);
			if (!Spectrum.HighlightsEnabled || !show)
				return null;
			int rows = grid.RowLow.Count,
				cols = grid.Columns.Count;
			var mask = new byte[rows * cols];

			if (Spectrum.HighlightMode == XGridHighlightMode.Max)
			{
				double max = XGridStats.MaxCellValue(grid, metric, Spectrum.DistanceWeighted);
				if (max <= 0)
					return mask;
				double threshold = XGridStats.MaxModeThreshold(max, percent);
				for (var r = 0; r < rows; r++)
				for (var c = 0; c < cols; c++)
				{
					double value = XGridStats.CellValue(grid, (r, c), metric, Spectrum.DistanceWeighted);
					if (value > 0 && value >= threshold)
						mask[r * cols + c] = 1;
				}
				return mask;
			}

			var order = XGridStats.CellOrder(grid, metric, Spectrum.DistanceWeighted);
			int n = order.Length;
			int count = XGridStats.TopCount(n, percent);
			for (var i = n - count; i < n; i++)
			{
				var (row, col) = order[i];
				mask[row * cols + col] = 1;
			}
			return mask;
		}

		// Same shape as TopHighlightMask for the signed Delta metric, sliced from either end
		// (positive: buy-heavy, negative: sell-heavy) and clipped to its own sign.
		private byte[]? DeltaHighlightMask(XGridResult grid, bool positive)
		{
			bool show = positive ? Spectrum.DeltaPosShow : Spectrum.DeltaNegShow;
			double percent = positive ? Spectrum.DeltaPosPercent : Spectrum.DeltaNegPercent;
			if (!Spectrum.HighlightsEnabled || !show)
				return null;
			int rows = grid.RowLow.Count,
				cols = grid.Columns.Count;
			var mask = new byte[rows * cols];

			if (Spectrum.HighlightMode == XGridHighlightMode.Max)
			{
				double max = XGridStats.MaxAbsDeltaOnSide(grid, positive, Spectrum.DistanceWeighted);
				if (max <= 0)
					return mask;
				double threshold = XGridStats.MaxModeThreshold(max, percent);
				for (var r = 0; r < rows; r++)
				for (var c = 0; c < cols; c++)
				{
					double value = XGridStats.CellValue(grid, (r, c), XGridSpectrumMetric.Delta, Spectrum.DistanceWeighted);
					bool matches = positive ? value >= threshold && value > 0 : -value >= threshold && value < 0;
					if (matches)
						mask[r * cols + c] = 1;
				}
				return mask;
			}

			var order = XGridStats.CellOrder(grid, XGridSpectrumMetric.Delta, Spectrum.DistanceWeighted);
			int n = order.Length;
			int count = XGridStats.TopCount(n, percent);
			int start = positive ? n - count : 0;
			int end = positive ? n : count;
			for (var i = start; i < end; i++)
			{
				var (row, col) = order[i];
				double value = XGridStats.CellValue(grid, (row, col), XGridSpectrumMetric.Delta, Spectrum.DistanceWeighted);
				if (positive ? value > 0 : value < 0)
					mask[row * cols + col] = 1;
			}
			return mask;
		}

		// Row-uniform -- the sweep metric has no per-column dimension (see
		// XGridStats.ComputeSweeps), so the whole row shades the same regardless of column.
		private double RowSweepValue(XGridResult grid, int row) =>
			Display.SweepMode switch
			{
				XGridSweepMode.LeftToRight => grid.SweepLeftToRight[row],
				XGridSweepMode.RightToLeft => grid.SweepRightToLeft[row],
				XGridSweepMode.Sum => grid.SweepSum[row],
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

		// Column indices where OpenTime crosses into a new hours-wide bucket counted from ticks
		// zero (midnight), so bucket edges land on absolute clock times, not the first column.
		private static IReadOnlyList<int> IntervalBoundaryColumns(XGridResult grid, int hours)
		{
			if (hours <= 0)
				return Array.Empty<int>();

			long BucketOf(DateTime t) => t.Ticks / (TimeSpan.TicksPerHour * hours);

			var cols = new List<int>();
			for (var c = 1; c < grid.Columns.Count; c++)
				if (BucketOf(grid.Columns[c].OpenTime) != BucketOf(grid.Columns[c - 1].OpenTime))
					cols.Add(c);
			return cols;
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
	}
}
