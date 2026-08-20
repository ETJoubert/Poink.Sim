using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Charts;

// One row x column intersection of XGrid. Value is the metric XGrid's cell colour is
// driven by -- Count stands in for it until X (the real signal) is decided; Count/
// UpCount/DownCount ride along so a future X can be computed without reshaping the grid.
// Public (not internal) since XGrid.razor.cs's [Parameter] Grid exposes it -- same reason
// TickVolumeProfileChart's WeightedPriceSample is public rather than internal.
public readonly record struct XGridCell(
    int Count, int UpCount, int DownCount, double UpDistance, double DownDistance, double Value);

// One bar's column -- Cells is row-ordered high-to-low, matching XGridResult's RowLow/RowHigh.
public readonly record struct XGridColumn(DateTime OpenTime, IReadOnlyList<XGridCell> Cells);

// A single reference price pinned to whichever XGrid row contains it -- Row indexes
// XGridResult.RowLow/RowHigh the same way XGridColumn.Cells does.
public readonly record struct XGridLevel(double Price, int Row);

// SweepLeftToRight[row]/SweepRightToLeft[row] -- how many sweep steps landed their
// window's POC (the row with the most ticks, at this grid's own row resolution) on that
// row. A step's window shrinks the Volume Range from one side while the other side stays
// pinned at the range's own edge (see XGridStats.Compute's sweep loops) -- this is what
// lets a dragged Volume Range border land on a different POC than the same border dragged
// from the opposite direction. SweepSum is the elementwise sum of the two.
//
// Vah/Val/Vwap/Average/Median mirror the same-named overlays ProfilerPage already draws
// on the main chart (see ProfileLevelLines/VwapPoints/AverageMedianLines) -- null when
// there isn't enough data for that particular level, same as those. Current is the range's
// latest bar close (its right-most price), pinned to whichever row holds it.
//
// NetByColumn is the running cumulative SBuy - SSell after each column, so its last element
// equals the SBuy - SSell readout -- one point per column, left to right in time.
//
// TotalPeak/UpPeak/DownPeak are this grid's own row-aggregated tick-count peak by that
// metric (summed across every column) -- the footprint's own POC-equivalent for Total/Up/
// Down, computed independently from Poc (which comes from VolumeProfileStats over
// WeightedPriceSample). The two can land on different rows, which is what the
// Is*AbovePoc/BelowPoc/AtPoc signals below compare. Null when every row is empty for
// that metric, same as the other levels.
public readonly record struct XGridResult(
    IReadOnlyList<double> RowLow, IReadOnlyList<double> RowHigh, IReadOnlyList<XGridColumn> Columns, double MaxValue,
    int MaxAbsDelta, double MaxDistanceValue, double MaxAbsDeltaDistance,
    double SBuy, double SSell, IReadOnlyList<double> NetByColumn,
    IReadOnlyList<double> SweepLeftToRight, IReadOnlyList<double> SweepRightToLeft, IReadOnlyList<double> SweepSum,
    double MaxSweepValue,
    XGridLevel? Poc, XGridLevel? Vah, XGridLevel? Val, XGridLevel? Vwap, XGridLevel? Average, XGridLevel? Median,
    XGridLevel? TotalPeak, XGridLevel? UpPeak, XGridLevel? DownPeak,
    XGridLevel? Current);

// Which per-cell metric XGridStats ranks cells by -- Total is the raw tick count
// (XGridCell.Value), Up/Down isolate one side's ticks so the two directions can be
// inspected independently.
internal enum XGridSpectrumMetric { Total, Up, Down }

// Footprint-style price/bar matrix -- rows are price buckets (shared across every column,
// same bucketing approach as VolumeProfileStats), columns are the bars inside ProfilerPage's
// Volume Range selection, grouped down to colCount columns when the selection holds more
// bars than that (each column then merges its group's histograms, same "sum the per-bucket
// counts" reduction BarPriceHistogram.Merge already does across a whole window). Kept
// separate from XGrid.razor so the component stays render-only (see MarketChart.
// README's three-layer split for the same reasoning applied to draw tools).
internal static class XGridStats
{
    // Safety cap on rows derived from a tiny rowHeight over a wide range -- rows past this
    // are dropped from the low end rather than shrinking rowHeight to fit.
    private const int MaxRows = 1000;

    public static XGridResult? Compute(
        IReadOnlyList<HlocAvdtBar> bars, IReadOnlyList<BarPriceHistogram> histograms,
        IReadOnlyList<WeightedPriceSample> samples, DateTime rangeStart, DateTime rangeEnd, double rowHeight, int colCount)
    {
        var indices = new List<int>();
        for (var i = 0; i < bars.Count; i++)
            if (bars[i].OpenTime >= rangeStart && bars[i].OpenTime <= rangeEnd)
                indices.Add(i);
        if (indices.Count == 0) return null;

        rowHeight = rowHeight > 0 ? rowHeight : double.Epsilon;
        colCount = Math.Clamp(colCount, 1, indices.Count);

        double min = double.MaxValue, max = double.MinValue;
        foreach (var i in indices)
        {
            var hist = histograms[i];
            foreach (var (key, _) in hist.Buckets)
            {
                var low = key * hist.BucketWidth;
                if (low < min) min = low;
                if (low + hist.BucketWidth > max) max = low + hist.BucketWidth;
            }
        }
        if (max <= min) return null;

        // Rows snap to absolute multiples of rowHeight (same floor(price/width) scheme
        // PriceCellTracker uses) instead of dividing this call's own range -- so row edges
        // stay fixed across renders and windows instead of drifting with whatever range this
        // particular call was given.
        long topCell = (long)Math.Floor(max / rowHeight);
        long bottomCell = (long)Math.Floor(min / rowHeight);
        int rowCount = (int)Math.Clamp(topCell - bottomCell + 1, 1, MaxRows);

        int RowOf(double price) => (int)Math.Clamp(topCell - (long)Math.Floor(price / rowHeight), 0, rowCount - 1);

        var rowLow = new List<double>(rowCount);
        var rowHigh = new List<double>(rowCount);
        for (var r = 0; r < rowCount; r++)
        {
            rowHigh.Add((topCell - r + 1) * rowHeight);
            rowLow.Add((topCell - r) * rowHeight);
        }

        var columns = new List<XGridColumn>(colCount);
        var maxValue = 0.0;
        var maxAbsDelta = 0;
        var maxDistanceValue = 0.0;
        var maxAbsDeltaDistance = 0.0;
        var sBuy = 0.0;
        var sSell = 0.0;
        var netByColumn = new List<double>(colCount);
        var rowTotalCounts = new double[rowCount];
        var rowUpCounts = new double[rowCount];
        var rowDownCounts = new double[rowCount];
        for (var g = 0; g < colCount; g++)
        {
            // Splits indices into colCount contiguous groups as evenly as integer division
            // allows -- same "distribute the remainder across the earliest groups" spread
            // as the rest of the codebase's bucketing.
            var groupStart = (int)((long)g * indices.Count / colCount);
            var groupEnd = (int)((long)(g + 1) * indices.Count / colCount);

            var counts = new int[rowCount];
            var upCounts = new int[rowCount];
            var downCounts = new int[rowCount];
            var upDistances = new double[rowCount];
            var downDistances = new double[rowCount];
            for (var gi = groupStart; gi < groupEnd; gi++)
            {
                var hist = histograms[indices[gi]];
                foreach ((long key, PriceBucketCounts c) in hist.Buckets)
                {
                    double price = key * hist.BucketWidth;
                    // 0 = top row (highest price), rowCount - 1 = bottom row (lowest price).
                    int row = RowOf(price);
                    counts[row] += c.Count;
                    upCounts[row] += c.UpCount;
                    downCounts[row] += c.DownCount;
                    upDistances[row] += c.UpDistance;
                    downDistances[row] += c.DownDistance;
                    sBuy += c.UpPriceSum;
                    sSell += c.DownPriceSum;
                }
            }

            var cells = new List<XGridCell>(rowCount);
            for (var r = 0; r < rowCount; r++)
            {
                double value = counts[r];
                if (value > maxValue) maxValue = value;
                var absDelta = Math.Abs(upCounts[r] - downCounts[r]);
                if (absDelta > maxAbsDelta) maxAbsDelta = absDelta;
                var distanceValue = upDistances[r] + downDistances[r];
                if (distanceValue > maxDistanceValue) maxDistanceValue = distanceValue;
                var absDeltaDistance = Math.Abs(upDistances[r] - downDistances[r]);
                if (absDeltaDistance > maxAbsDeltaDistance) maxAbsDeltaDistance = absDeltaDistance;
                cells.Add(new XGridCell(counts[r], upCounts[r], downCounts[r], upDistances[r], downDistances[r], value));
                rowTotalCounts[r] += counts[r];
                rowUpCounts[r] += upCounts[r];
                rowDownCounts[r] += downCounts[r];
            }
            netByColumn.Add(sBuy - sSell);
            columns.Add(new XGridColumn(bars[indices[groupStart]].OpenTime, cells));
        }

        var (sweepLtr, sweepRtl, sweepSum, maxSweepValue) = ComputeSweeps(histograms, indices, rowCount, RowOf);

        XGridLevel? ToLevel(double? price) => price is { } p ? new XGridLevel(p, RowOf(p)) : null;

        var profileLevels = VolumeProfileStats.Compute(samples);
        var poc = ToLevel(profileLevels?.Poc);
        var vah = ToLevel(profileLevels?.ValueAreaHigh);
        var val = ToLevel(profileLevels?.ValueAreaLow);
        var average = ToLevel(VolumeProfileStats.ComputeAverage(samples));
        var median = ToLevel(VolumeProfileStats.ComputeMedian(samples));

        double totalVolume = 0, totalPriceVolume = 0;
        foreach (var i in indices)
        {
            totalPriceVolume += bars[i].Average * bars[i].Volume;
            totalVolume += bars[i].Volume;
        }
        var vwap = ToLevel(totalVolume > 0 ? (double?)(totalPriceVolume / totalVolume) : null);
        var current = ToLevel(bars[indices[^1]].Close);

        XGridLevel? PeakLevel(double[] rowSums)
        {
            var best = -1;
            var bestValue = 0.0;
            for (var r = 0; r < rowSums.Length; r++)
                if (rowSums[r] > bestValue) { bestValue = rowSums[r]; best = r; }
            return best < 0 ? null : new XGridLevel((rowLow[best] + rowHigh[best]) / 2, best);
        }
        var totalPeak = PeakLevel(rowTotalCounts);
        var upPeak = PeakLevel(rowUpCounts);
        var downPeak = PeakLevel(rowDownCounts);

        return new XGridResult(
            rowLow, rowHigh, columns, maxValue, maxAbsDelta, maxDistanceValue, maxAbsDeltaDistance,
            sBuy, sSell, netByColumn,
            sweepLtr, sweepRtl, sweepSum, maxSweepValue,
            poc, vah, val, vwap, average, median, totalPeak, upPeak, downPeak, current);
    }

    // Walks every possible position of the Volume Range's near border, one raw bar at a
    // time (independent of colCount's column grouping -- a dragged border moves bar by
    // bar, not group by group), tallying which row each resulting window's POC lands on.
    // L->R pins the right edge at indices[^1] and sweeps the left edge indices[0]..indices[^1]
    // (window [i, ^1]); R->L pins the left edge at indices[0] and sweeps the right edge the
    // same span (window [0, i]). Prefix/suffix sums keep this O(bars x rowCount) instead of
    // re-summing each window from scratch.
    private static (double[] Ltr, double[] Rtl, double[] Sum, double MaxSum) ComputeSweeps(
        IReadOnlyList<BarPriceHistogram> histograms, List<int> indices, int rowCount, Func<double, int> rowOf)
    {
        var barRowCounts = new double[indices.Count][];
        for (var i = 0; i < indices.Count; i++)
        {
            var hist = histograms[indices[i]];
            var rc = new double[rowCount];
            foreach ((long key, PriceBucketCounts c) in hist.Buckets)
            {
                double price = key * hist.BucketWidth;
                rc[rowOf(price)] += c.Count;
            }
            barRowCounts[i] = rc;
        }

        var sweepLtr = new double[rowCount];
        var suffix = new double[rowCount];
        for (var i = indices.Count - 1; i >= 0; i--)
        {
            for (var r = 0; r < rowCount; r++) suffix[r] += barRowCounts[i][r];
            sweepLtr[ArgMax(suffix)]++;
        }

        var sweepRtl = new double[rowCount];
        var prefix = new double[rowCount];
        for (var i = 0; i < indices.Count; i++)
        {
            for (var r = 0; r < rowCount; r++) prefix[r] += barRowCounts[i][r];
            sweepRtl[ArgMax(prefix)]++;
        }

        var sweepSum = new double[rowCount];
        var maxSweepValue = 0.0;
        for (var r = 0; r < rowCount; r++)
        {
            sweepSum[r] = sweepLtr[r] + sweepRtl[r];
            if (sweepSum[r] > maxSweepValue) maxSweepValue = sweepSum[r];
        }

        return (sweepLtr, sweepRtl, sweepSum, maxSweepValue);
    }

    private static int ArgMax(double[] values)
    {
        var best = 0;
        for (var i = 1; i < values.Length; i++)
            if (values[i] > values[best]) best = i;
        return best;
    }

    // The cell the price is at right now -- current price's row, most recent (right-most)
    // column. Null when there's no Current level or the grid has no columns.
    public static (int Row, int Col)? CurrentCell(XGridResult grid) =>
        grid.Current is { } cur && grid.Columns.Count > 0 ? (cur.Row, grid.Columns.Count - 1) : null;

    // True when the current cell's row matches the given level's row -- false when there's
    // no current cell, or the level itself isn't set (not enough data for that level).
    private static bool IsCurrentAtLevel(XGridResult grid, XGridLevel? level) =>
        CurrentCell(grid) is { } cell && level is { } l && l.Row == cell.Row;

    public static bool IsCurrentAtPoc(XGridResult grid) => IsCurrentAtLevel(grid, grid.Poc);
    public static bool IsCurrentAtVah(XGridResult grid) => IsCurrentAtLevel(grid, grid.Vah);
    public static bool IsCurrentAtVal(XGridResult grid) => IsCurrentAtLevel(grid, grid.Val);
    public static bool IsCurrentAtVwap(XGridResult grid) => IsCurrentAtLevel(grid, grid.Vwap);

    // Above/below compare actual price, not row -- finer-grained than IsCurrentAtLevel's row
    // match, since a row is a bucket that can span many price levels. False when there's no
    // current price, or the level itself isn't set.
    private static bool IsCurrentAboveLevel(XGridResult grid, XGridLevel? level) =>
        grid.Current is { } cur && level is { } l && cur.Price > l.Price;

    private static bool IsCurrentBelowLevel(XGridResult grid, XGridLevel? level) =>
        grid.Current is { } cur && level is { } l && cur.Price < l.Price;

    public static bool IsCurrentAbovePoc(XGridResult grid) => IsCurrentAboveLevel(grid, grid.Poc);
    public static bool IsCurrentBelowPoc(XGridResult grid) => IsCurrentBelowLevel(grid, grid.Poc);
    public static bool IsCurrentAboveVah(XGridResult grid) => IsCurrentAboveLevel(grid, grid.Vah);
    public static bool IsCurrentBelowVah(XGridResult grid) => IsCurrentBelowLevel(grid, grid.Vah);
    public static bool IsCurrentAboveVal(XGridResult grid) => IsCurrentAboveLevel(grid, grid.Val);
    public static bool IsCurrentBelowVal(XGridResult grid) => IsCurrentBelowLevel(grid, grid.Val);
    public static bool IsCurrentAboveVwap(XGridResult grid) => IsCurrentAboveLevel(grid, grid.Vwap);
    public static bool IsCurrentBelowVwap(XGridResult grid) => IsCurrentBelowLevel(grid, grid.Vwap);

    // Level-vs-level comparison -- same price-for-above/below, row-for-at split as
    // IsCurrentAboveLevel/IsCurrentAtLevel, but between two computed levels instead of
    // between the current price and one level.
    private static bool IsLevelAboveLevel(XGridLevel? level, XGridLevel? reference) =>
        level is { } l && reference is { } r && l.Price > r.Price;

    private static bool IsLevelBelowLevel(XGridLevel? level, XGridLevel? reference) =>
        level is { } l && reference is { } r && l.Price < r.Price;

    private static bool IsLevelAtLevel(XGridLevel? level, XGridLevel? reference) =>
        level is { } l && reference is { } r && l.Row == r.Row;

    public static bool IsVwapAbovePoc(XGridResult grid) => IsLevelAboveLevel(grid.Vwap, grid.Poc);
    public static bool IsVwapBelowPoc(XGridResult grid) => IsLevelBelowLevel(grid.Vwap, grid.Poc);
    public static bool IsVwapAtPoc(XGridResult grid) => IsLevelAtLevel(grid.Vwap, grid.Poc);

    public static bool IsTotalAbovePoc(XGridResult grid) => IsLevelAboveLevel(grid.TotalPeak, grid.Poc);
    public static bool IsTotalBelowPoc(XGridResult grid) => IsLevelBelowLevel(grid.TotalPeak, grid.Poc);
    public static bool IsTotalAtPoc(XGridResult grid) => IsLevelAtLevel(grid.TotalPeak, grid.Poc);

    public static bool IsUpAbovePoc(XGridResult grid) => IsLevelAboveLevel(grid.UpPeak, grid.Poc);
    public static bool IsUpBelowPoc(XGridResult grid) => IsLevelBelowLevel(grid.UpPeak, grid.Poc);
    public static bool IsUpAtPoc(XGridResult grid) => IsLevelAtLevel(grid.UpPeak, grid.Poc);

    public static bool IsDownAbovePoc(XGridResult grid) => IsLevelAboveLevel(grid.DownPeak, grid.Poc);
    public static bool IsDownBelowPoc(XGridResult grid) => IsLevelBelowLevel(grid.DownPeak, grid.Poc);
    public static bool IsDownAtPoc(XGridResult grid) => IsLevelAtLevel(grid.DownPeak, grid.Poc);

    public static double CellValue(XGridResult grid, (int Row, int Col) cell, XGridSpectrumMetric metric)
    {
        XGridCell c = grid.Columns[cell.Col].Cells[cell.Row];
        return metric switch
        {
            XGridSpectrumMetric.Up => c.UpCount,
            XGridSpectrumMetric.Down => c.DownCount,
            _ => c.Value,
        };
    }

    // Every (row, col) cell with a nonzero metric value, ordered least to most by it. Cells
    // at zero (no ticks at all for Total, or none on that side for Up/Down) are excluded
    // rather than sorted to the front -- a footprint grid is mostly empty, so including them
    // would flatten the curve into a long zero run. Sort order among equal values is
    // unspecified (Array.Sort isn't stable), but that only affects the curve's shape at a
    // plateau, not which point marks the current cell or the top-percent threshold -- both
    // look up by (Row, Col) identity or by rank, not by value.
    public static (int Row, int Col)[] CellOrder(XGridResult grid, XGridSpectrumMetric metric)
    {
        int rows = grid.RowLow.Count, cols = grid.Columns.Count;
        var cells = new List<(int Row, int Col)>(rows * cols);
        for (var c = 0; c < cols; c++)
            for (var r = 0; r < rows; r++)
                if (CellValue(grid, (r, c), metric) > 0) cells.Add((r, c));

        var order = cells.ToArray();
        Array.Sort(order, (a, b) => CellValue(grid, a, metric).CompareTo(CellValue(grid, b, metric)));
        return order;
    }

    // How many of the n nonzero cells fall within the top Percent% by value.
    public static int TopCount(int n, double percent) =>
        Math.Clamp((int)Math.Round(n * Math.Clamp(percent, 0, 100) / 100.0, MidpointRounding.AwayFromZero), 0, n);

    // True when the current (latest) cell's metric value ranks inside the top percent% of
    // every nonzero cell on the grid. False when there's no current cell, or the current
    // cell itself has a zero metric value (excluded from CellOrder, so it can never rank).
    public static bool IsCurrentInTopPercent(XGridResult grid, XGridSpectrumMetric metric, double percent)
    {
        if (CurrentCell(grid) is not { } cell) return false;
        var order = CellOrder(grid, metric);
        int rank = Array.IndexOf(order, cell);
        if (rank < 0) return false;

        int n = order.Length;
        return rank >= n - TopCount(n, percent);
    }
}

// A signal is one boolean condition over a grid -- Key is its stable identity (used as the
// RL feature name and as the Brain Builder's persisted toggle), Group/Label/Tooltip drive the
// Brain Builder checkbox UI, Predicate is the condition itself. One catalog entry is the whole
// cost of adding a signal; nothing else in the codebase needs a matching case.
internal delegate bool XGridSignalPredicate(XGridResult grid, double percent);

internal sealed record XGridSignalDef(string Key, string Group, string Label, string Tooltip, XGridSignalPredicate Predicate);

// Shared by the Brain Builder's XGrid input encoder and any future live overlay toggle, so
// neither has to re-derive the signal list.
internal static class XGridSignalCatalog
{
    // Well-known keys referenced by identity elsewhere in code (mask rules) -- everything else
    // is only ever looked up by iterating All, so it doesn't need a named constant here.
    public static class Keys
    {
        public const string RankTopPercentUp = "rank-top-up";
        public const string RankTopPercentDown = "rank-top-down";
        public const string AtPoc = "at-poc";
        public const string AtVah = "at-vah";
        public const string AtVal = "at-val";
        public const string AtVwap = "at-vwap";
    }

    public static readonly IReadOnlyList<XGridSignalDef> All = new[]
    {
        new XGridSignalDef("rank-top-total", "Rank", "Top % Total",
            "Current cell's total tick count ranks in the top % of every nonzero cell on the grid",
            (g, pct) => XGridStats.IsCurrentInTopPercent(g, XGridSpectrumMetric.Total, pct)),
        new XGridSignalDef(Keys.RankTopPercentUp, "Rank", "Top % Up",
            "Current cell's up-tick count ranks in the top % of every nonzero cell (Up metric)",
            (g, pct) => XGridStats.IsCurrentInTopPercent(g, XGridSpectrumMetric.Up, pct)),
        new XGridSignalDef(Keys.RankTopPercentDown, "Rank", "Top % Down",
            "Current cell's down-tick count ranks in the top % of every nonzero cell (Down metric)",
            (g, pct) => XGridStats.IsCurrentInTopPercent(g, XGridSpectrumMetric.Down, pct)),

        new XGridSignalDef(Keys.AtPoc, "At", "At POC", "Current price's row matches the Point of Control's row",
            (g, _) => XGridStats.IsCurrentAtPoc(g)),
        new XGridSignalDef(Keys.AtVah, "At", "At VAH", "Current price's row matches the Value Area High's row",
            (g, _) => XGridStats.IsCurrentAtVah(g)),
        new XGridSignalDef(Keys.AtVal, "At", "At VAL", "Current price's row matches the Value Area Low's row",
            (g, _) => XGridStats.IsCurrentAtVal(g)),
        new XGridSignalDef(Keys.AtVwap, "At", "At VWAP", "Current price's row matches the VWAP's row",
            (g, _) => XGridStats.IsCurrentAtVwap(g)),

        new XGridSignalDef("above-poc", "Above", "Above POC", "Current price is above the Point of Control's price",
            (g, _) => XGridStats.IsCurrentAbovePoc(g)),
        new XGridSignalDef("above-vah", "Above", "Above VAH", "Current price is above the Value Area High's price",
            (g, _) => XGridStats.IsCurrentAboveVah(g)),
        new XGridSignalDef("above-val", "Above", "Above VAL", "Current price is above the Value Area Low's price",
            (g, _) => XGridStats.IsCurrentAboveVal(g)),
        new XGridSignalDef("above-vwap", "Above", "Above VWAP", "Current price is above the VWAP's price",
            (g, _) => XGridStats.IsCurrentAboveVwap(g)),

        new XGridSignalDef("below-poc", "Below", "Below POC", "Current price is below the Point of Control's price",
            (g, _) => XGridStats.IsCurrentBelowPoc(g)),
        new XGridSignalDef("below-vah", "Below", "Below VAH", "Current price is below the Value Area High's price",
            (g, _) => XGridStats.IsCurrentBelowVah(g)),
        new XGridSignalDef("below-val", "Below", "Below VAL", "Current price is below the Value Area Low's price",
            (g, _) => XGridStats.IsCurrentBelowVal(g)),
        new XGridSignalDef("below-vwap", "Below", "Below VWAP", "Current price is below the VWAP's price",
            (g, _) => XGridStats.IsCurrentBelowVwap(g)),

        new XGridSignalDef("vwap-at-poc", "Vs POC", "VWAP At POC", "VWAP's row matches the Point of Control's row",
            (g, _) => XGridStats.IsVwapAtPoc(g)),
        new XGridSignalDef("vwap-above-poc", "Vs POC", "VWAP Above POC", "VWAP's price is above the Point of Control's price",
            (g, _) => XGridStats.IsVwapAbovePoc(g)),
        new XGridSignalDef("vwap-below-poc", "Vs POC", "VWAP Below POC", "VWAP's price is below the Point of Control's price",
            (g, _) => XGridStats.IsVwapBelowPoc(g)),

        new XGridSignalDef("total-at-poc", "Vs POC", "Total At POC",
            "The grid's own total tick-count peak row matches the Point of Control's row",
            (g, _) => XGridStats.IsTotalAtPoc(g)),
        new XGridSignalDef("total-above-poc", "Vs POC", "Total Above POC",
            "The grid's own total tick-count peak row is above the Point of Control's price",
            (g, _) => XGridStats.IsTotalAbovePoc(g)),
        new XGridSignalDef("total-below-poc", "Vs POC", "Total Below POC",
            "The grid's own total tick-count peak row is below the Point of Control's price",
            (g, _) => XGridStats.IsTotalBelowPoc(g)),

        new XGridSignalDef("up-at-poc", "Vs POC", "Up At POC",
            "The grid's own up-tick peak row matches the Point of Control's row",
            (g, _) => XGridStats.IsUpAtPoc(g)),
        new XGridSignalDef("up-above-poc", "Vs POC", "Up Above POC",
            "The grid's own up-tick peak row is above the Point of Control's price",
            (g, _) => XGridStats.IsUpAbovePoc(g)),
        new XGridSignalDef("up-below-poc", "Vs POC", "Up Below POC",
            "The grid's own up-tick peak row is below the Point of Control's price",
            (g, _) => XGridStats.IsUpBelowPoc(g)),

        new XGridSignalDef("down-at-poc", "Vs POC", "Down At POC",
            "The grid's own down-tick peak row matches the Point of Control's row",
            (g, _) => XGridStats.IsDownAtPoc(g)),
        new XGridSignalDef("down-above-poc", "Vs POC", "Down Above POC",
            "The grid's own down-tick peak row is above the Point of Control's price",
            (g, _) => XGridStats.IsDownAbovePoc(g)),
        new XGridSignalDef("down-below-poc", "Vs POC", "Down Below POC",
            "The grid's own down-tick peak row is below the Point of Control's price",
            (g, _) => XGridStats.IsDownBelowPoc(g)),
    };

    private static readonly Dictionary<string, XGridSignalDef> ByKey = All.ToDictionary(d => d.Key);

    public static XGridSignalDef Get(string key) => ByKey[key];

    public static bool Contains(string key) => ByKey.ContainsKey(key);

    public static bool Value(XGridResult grid, string key, double percent) => Get(key).Predicate(grid, percent);
}
