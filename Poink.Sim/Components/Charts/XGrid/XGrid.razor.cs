using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Poink.Sim.Components.Charts
{
    // Data/pixels split: every row/column/cell value arrives pre-computed via Grid (see
    // XGridStats.Compute); this component turns the active display state into a flat numeric
    // payload and hands it to xGrid.js, which paints the footprint onto one canvas instead
    // of one DOM node per cell. Only the grid cells/overlays/markers moved to canvas -- the
    // header, toggles, totals and Net bar chart stay declarative markup.
    public partial class XGrid : ComponentBase, IAsyncDisposable
    {
        [Inject] private IJSRuntime JSRuntime { get; set; } = null!;

        [Parameter] public XGridResult? Grid { get; set; }

        // Two-way bound so ProfilerPage (the only one that can call XGridStats.Compute --
        // it owns the bars/histograms) recomputes Grid when either changes. The inputs
        // themselves live in this component's own header, same ownership split as
        // TickVolumeProfileChart/TickTimeProfileChart's Collapsed parameter.
        [Parameter] public int RowCount { get; set; } = 20;
        [Parameter] public EventCallback<int> RowCountChanged { get; set; }

        [Parameter] public int ColCount { get; set; } = 20;
        [Parameter] public EventCallback<int> ColCountChanged { get; set; }

        private Task SetRowCount(int value) => RowCountChanged.InvokeAsync(RowCount = value);
        private Task SetColCount(int value) => ColCountChanged.InvokeAsync(ColCount = value);

        // Two-way bound so the parent shrinks its own layout column around this panel, same
        // ownership split as TickVolumeProfileChart/TickTimeProfileChart's Collapsed.
        [Parameter] public bool Collapsed { get; set; }
        [Parameter] public EventCallback<bool> CollapsedChanged { get; set; }

        private Task ToggleCollapsed() => CollapsedChanged.InvokeAsync(Collapsed = !Collapsed);

        // Local overlay state, same fixed-position "get bigger" expand as MarketChart's own
        // _expanded -- purely visual, so it stays unbound rather than a two-way parameter.
        private bool _expanded;

        private void ToggleExpanded() => _expanded = !_expanded;

        // Host-provided controls shown in a top bar only while expanded -- lets the hosting
        // page drop mini playback/profile controls beside the fullscreen grid. Extensible by
        // design: the host owns the content, so adding controls needs no change here.
        [Parameter] public RenderFragment? ExpandedControls { get; set; }

        // Which sweep metric shades on top of the base tick-count grid -- purely a display
        // choice (all three are already sitting on Grid), so it stays local, unbound state
        // rather than a two-way parameter like RowCount/ColCount, which do drive a
        // recompute. Opt-in: None is the default, so the grid starts unshaded.
        private enum SweepMode { None, LeftToRight, RightToLeft, Sum }

        private SweepMode _sweepMode = SweepMode.None;

        // Opt-in delta shading -- recolours each cell by its up-minus-down tick balance
        // instead of its raw tick count, same "display choice sitting on data already on
        // Grid" rationale as _sweepMode.
        private bool _shadeDelta;

        // Weights each tick by the price distance it moved instead of counting it as one --
        // modifies whichever shading is active (count or delta), same opt-in display-only
        // rationale as _sweepMode/_shadeDelta.
        private bool _distanceWeighted;

        // Opt-in line graph of NetByColumn under the totals row -- default off like every
        // other overlay.
        private bool _showNetGraph;

        // Poc/Vah/Val/Vwap/Average/Median are independently toggleable, same "each gets its
        // own checkbox" convention as ProfilerPage's own CHART OVERLAYS panel. Every overlay
        // starts off so the grid opens unshaded and unmarked.
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
                    if (_disposed) return;
                    _dotNetRef = DotNetObjectReference.Create(this);
                }

                // The canvas is only in the DOM when this same condition rendered it, so
                // _canvasRef is valid exactly then -- guarding here keeps draw off a stale ref.
                if (!_disposed && !Collapsed && Grid is { } grid)
                    await _module.InvokeVoidAsync("draw", _canvasRef, _dotNetRef, BuildPayload(grid));
            }
            catch (JSDisconnectedException) { }
            catch (ObjectDisposedException) { }
        }

        // One byte per cell, 128 = zero, sign carried for delta mode -- keeps the per-cell
        // payload to a byte[] (base64 fast path over interop) instead of a colour string per
        // cell. The intensity mapping mirrors what the CSS-grid version computed inline.
        private object BuildPayload(XGridResult grid)
        {
            int rows = grid.RowLow.Count, cols = grid.Columns.Count;
            var cells = new byte[rows * cols];
            for (var r = 0; r < rows; r++)
                for (var c = 0; c < cols; c++)
                    cells[r * cols + c] = EncodeIntensity(CellIntensity(grid, r, c));

            double[]? sweep = null;
            if (_sweepMode != SweepMode.None)
            {
                sweep = new double[rows];
                for (var r = 0; r < rows; r++) sweep[r] = RowSweepIntensity(grid, r);
            }

            var markers = new List<object>();
            void Add(bool show, XGridLevel? level, string kind)
            {
                if (show && level is { } l) markers.Add(new { row = l.Row, kind });
            }
            Add(_showPoc, grid.Poc, "poc");
            Add(_showVah, grid.Vah, "vah");
            Add(_showVal, grid.Val, "val");
            Add(_showVwap, grid.Vwap, "vwap");
            Add(_showAverage, grid.Average, "avg");
            Add(_showMedian, grid.Median, "med");

            return new
            {
                rows,
                cols,
                mode = _shadeDelta ? "delta" : "count",
                cells,
                sweep,
                markers,
                current = grid.Current is { } cur ? (int?)cur.Row : null,
            };
        }

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

        private static byte EncodeIntensity(double v) =>
            (byte)Math.Clamp((int)Math.Round(128 + v * 127), 0, 255);

        [JSInvokable]
        public string CellTooltip(int row, int col)
        {
            if (Grid is not { } grid) return "";
            if (row < 0 || col < 0 || row >= grid.RowLow.Count || col >= grid.Columns.Count) return "";

            XGridCell cell = grid.Columns[col].Cells[row];
            var text = $"{grid.RowLow[row]:F5} - {grid.RowHigh[row]:F5} @ {grid.Columns[col].OpenTime:HH:mm}: " +
                       $"{cell.Count} ticks ({cell.UpCount} up / {cell.DownCount} down)";

            if (_sweepMode != SweepMode.None)
                text += $" | POC on {RowSweepValue(grid, row):0} sweep steps";

            var levels = new List<string>();
            void Note(bool show, XGridLevel? level, string label)
            {
                if (show && level is { } l && l.Row == row) levels.Add(label);
            }
            Note(_showPoc, grid.Poc, "POC");
            Note(_showVah, grid.Vah, "VAH");
            Note(_showVal, grid.Val, "VAL");
            Note(_showVwap, grid.Vwap, "VWAP");
            Note(_showAverage, grid.Average, "AVG");
            Note(_showMedian, grid.Median, "MED");
            if (grid.Current is { } current && current.Row == row) levels.Add("current");
            if (levels.Count > 0) text += " | " + string.Join(", ", levels);

            return text;
        }

        public async ValueTask DisposeAsync()
        {
            _disposed = true;
            if (_module is not null)
            {
                try { await _module.DisposeAsync(); }
                catch (JSDisconnectedException) { }
                catch (ObjectDisposedException) { }
            }
            _dotNetRef?.Dispose();
            GC.SuppressFinalize(this);
        }

        private readonly record struct NetBar(string X, string Y, string Width, string Height, bool Positive);

        // One bar per column, aligned to the grid's equal-width columns and drawn from the
        // zero baseline: positive Net rises above it, negative drops below. Mapped onto the
        // same 0..100 viewBox the baseline/crossings use, stretched to fill width.
        private static IReadOnlyList<NetBar> NetGraphBars(XGridResult grid)
        {
            var series = grid.NetByColumn;
            var bars = new List<NetBar>(series.Count);
            if (series.Count == 0) return bars;

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
                bars.Add(new NetBar(XForm(i * slot + gap / 2), XForm(top), XForm(width), XForm(Math.Abs(zeroY - valueY)), value >= 0));
            }
            return bars;
        }

        private static string NetGraphZeroY(XGridResult grid)
        {
            var (min, range) = NetGraphSpan(grid.NetByColumn);
            double y = range <= 0 ? 50 : 100 - (0 - min) / range * 100;
            return y.ToString("0.##", CultureInfo.InvariantCulture);
        }

        // Every x (0..100 viewBox) where the cumulative Net line touches or crosses zero:
        // an exact zero point contributes its own x, a sign change between two columns
        // contributes the linearly interpolated crossing.
        private static IReadOnlyList<string> NetGraphZeroCrossings(XGridResult grid)
        {
            var series = grid.NetByColumn;
            var crossings = new List<string>();
            if (series.Count < 2) return crossings;

            for (var i = 1; i < series.Count; i++)
            {
                double a = series[i - 1], b = series[i];
                double xa = (double)(i - 1) / (series.Count - 1) * 100;
                double xb = (double)i / (series.Count - 1) * 100;
                if (a == 0) crossings.Add(XForm(xa));
                if ((a < 0 && b > 0) || (a > 0 && b < 0)) crossings.Add(XForm(xa + a / (a - b) * (xb - xa)));
            }
            if (series[^1] == 0) crossings.Add(XForm(100));
            return crossings;
        }

        private static string XForm(double x) => x.ToString("0.##", CultureInfo.InvariantCulture);

        private static (double Min, double Range) NetGraphSpan(IReadOnlyList<double> series)
        {
            double min = 0, max = 0;
            foreach (var v in series)
            {
                if (v < min) min = v;
                if (v > max) max = v;
            }
            return (min, max - min);
        }

        // Row-uniform -- the sweep metric has no per-column dimension (see
        // XGridStats.ComputeSweeps), so the whole row shades the same regardless of column.
        private double RowSweepValue(XGridResult grid, int row) => _sweepMode switch
        {
            SweepMode.LeftToRight => grid.SweepLeftToRight[row],
            SweepMode.RightToLeft => grid.SweepRightToLeft[row],
            SweepMode.Sum => grid.SweepSum[row],
            _ => 0,
        };

        private double RowSweepIntensity(XGridResult grid, int row) =>
            grid.MaxSweepValue <= 0 ? 0 : RowSweepValue(grid, row) / grid.MaxSweepValue;
    }
}
