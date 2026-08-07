using System;
using System.Collections.Generic;
using System.Globalization;
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

        // Two-way bound so the parent shrinks its own layout column around this panel, same
        // ownership split as TickVolumeProfileChart/TickTimeProfileChart's Collapsed.
        [Parameter] public bool Collapsed { get; set; }
        [Parameter] public EventCallback<bool> CollapsedChanged { get; set; }

        private Task ToggleCollapsed() => CollapsedChanged.InvokeAsync(Collapsed = !Collapsed);

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

        // Delta mode swaps the tick-count fill for an up/down balance fill: green when up
        // ticks lead, red when down ticks lead, opacity scaled by the imbalance relative to
        // the grid's largest imbalance so the strongest cell reads at full colour. The
        // distance multiplier swaps each tick's weight of one for the price distance it
        // moved, so both fill and ceiling switch to the matching distance totals.
        private string CellBackground(XGridResult grid, int row, int col)
        {
            XGridCell cell = grid.Columns[col].Cells[row];
            if (_shadeDelta)
            {
                double delta = _distanceWeighted ? cell.UpDistance - cell.DownDistance : cell.UpCount - cell.DownCount;
                double ceiling = _distanceWeighted ? grid.MaxAbsDeltaDistance : grid.MaxAbsDelta;
                string color = delta >= 0 ? "var(--color-success)" : "var(--color-error)";
                return Fill(color, ceiling <= 0 ? 0 : Math.Abs(delta) / ceiling);
            }

            double value = _distanceWeighted ? cell.UpDistance + cell.DownDistance : cell.Value;
            double maxValue = _distanceWeighted ? grid.MaxDistanceValue : grid.MaxValue;
            return Fill("var(--color-info)", maxValue <= 0 ? 0 : value / maxValue);
        }

        private static string Fill(string color, double intensity) =>
            $"color-mix(in srgb, {color} {(intensity * 100).ToString("0", CultureInfo.InvariantCulture)}%, transparent)";

        // Maps the cumulative Net series onto a 0..100 viewBox, stretched to fill width and
        // flipped so higher Net sits higher. Zero is always inside the range since the span
        // is seeded from zero (see NetGraphSpan), letting the baseline share the same mapping.
        private static string NetGraphPoints(XGridResult grid)
        {
            var series = grid.NetByColumn;
            var (min, range) = NetGraphSpan(series);
            var points = new string[series.Count];
            for (var i = 0; i < series.Count; i++)
            {
                double x = series.Count <= 1 ? 0 : (double)i / (series.Count - 1) * 100;
                double y = range <= 0 ? 50 : 100 - (series[i] - min) / range * 100;
                points[i] = $"{x.ToString("0.##", CultureInfo.InvariantCulture)},{y.ToString("0.##", CultureInfo.InvariantCulture)}";
            }
            return string.Join(" ", points);
        }

        private static string NetGraphZeroY(XGridResult grid)
        {
            var (min, range) = NetGraphSpan(grid.NetByColumn);
            double y = range <= 0 ? 50 : 100 - (0 - min) / range * 100;
            return y.ToString("0.##", CultureInfo.InvariantCulture);
        }

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
