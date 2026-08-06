using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Charts
{
    // Pure render: every row/column/cell value arrives pre-computed via Grid (see
    // XGridStats.Compute) -- this component holds no bucketing or aggregation logic of
    // its own, so its job stays limited to mapping XGridResult onto a CSS grid.
    public partial class XGrid : ComponentBase
    {
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

        // Which sweep metric shades on top of the base tick-count grid -- purely a display
        // choice (all three are already sitting on Grid), so it stays local, unbound state
        // rather than a two-way parameter like RowCount/ColCount, which do drive a
        // recompute. Opt-in: None is the default, so the grid starts unshaded.
        private enum SweepMode { None, LeftToRight, RightToLeft, Sum }

        private SweepMode _sweepMode = SweepMode.None;

        // Vah/Val/Vwap/Average/Median are independently toggleable, same "each gets its
        // own checkbox" convention as ProfilerPage's own CHART OVERLAYS panel.
        private bool _showVah = true;
        private bool _showVal = true;
        private bool _showVwap = true;
        private bool _showAverage = true;
        private bool _showMedian = true;

        private double CellIntensity(XGridResult grid, int row, int col) =>
            grid.MaxValue <= 0 ? 0 : grid.Columns[col].Cells[row].Value / grid.MaxValue;

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
