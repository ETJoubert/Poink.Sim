using System;
using System.Collections.Generic;
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
public readonly record struct XGridResult(
    IReadOnlyList<double> RowLow, IReadOnlyList<double> RowHigh, IReadOnlyList<XGridColumn> Columns, double MaxValue,
    int MaxAbsDelta, double MaxDistanceValue, double MaxAbsDeltaDistance,
    double SBuy, double SSell, IReadOnlyList<double> NetByColumn,
    IReadOnlyList<double> SweepLeftToRight, IReadOnlyList<double> SweepRightToLeft, IReadOnlyList<double> SweepSum,
    double MaxSweepValue,
    XGridLevel? Poc, XGridLevel? Vah, XGridLevel? Val, XGridLevel? Vwap, XGridLevel? Average, XGridLevel? Median,
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

        return new XGridResult(
            rowLow, rowHigh, columns, maxValue, maxAbsDelta, maxDistanceValue, maxAbsDeltaDistance,
            sBuy, sSell, netByColumn,
            sweepLtr, sweepRtl, sweepSum, maxSweepValue,
            poc, vah, val, vwap, average, median, current);
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

internal enum XGridSignalKind
{
    RankTopPercentTotal, RankTopPercentUp, RankTopPercentDown,
    AtPoc, AtVah, AtVal, AtVwap,
    AbovePoc, AboveVah, AboveVal, AboveVwap,
    BelowPoc, BelowVah, BelowVal, BelowVwap,
}

// One dispatch table for "which XGridStats predicate does this signal kind mean" -- shared
// by the Brain Builder's XGrid input encoder and any future live overlay toggle, so neither
// has to re-derive the mapping.
internal static class XGridSignals
{
    public static bool Value(XGridResult grid, XGridSignalKind kind, double percent) => kind switch
    {
        XGridSignalKind.RankTopPercentTotal => XGridStats.IsCurrentInTopPercent(grid, XGridSpectrumMetric.Total, percent),
        XGridSignalKind.RankTopPercentUp => XGridStats.IsCurrentInTopPercent(grid, XGridSpectrumMetric.Up, percent),
        XGridSignalKind.RankTopPercentDown => XGridStats.IsCurrentInTopPercent(grid, XGridSpectrumMetric.Down, percent),
        XGridSignalKind.AtPoc => XGridStats.IsCurrentAtPoc(grid),
        XGridSignalKind.AtVah => XGridStats.IsCurrentAtVah(grid),
        XGridSignalKind.AtVal => XGridStats.IsCurrentAtVal(grid),
        XGridSignalKind.AtVwap => XGridStats.IsCurrentAtVwap(grid),
        XGridSignalKind.AbovePoc => XGridStats.IsCurrentAbovePoc(grid),
        XGridSignalKind.AboveVah => XGridStats.IsCurrentAboveVah(grid),
        XGridSignalKind.AboveVal => XGridStats.IsCurrentAboveVal(grid),
        XGridSignalKind.AboveVwap => XGridStats.IsCurrentAboveVwap(grid),
        XGridSignalKind.BelowPoc => XGridStats.IsCurrentBelowPoc(grid),
        XGridSignalKind.BelowVah => XGridStats.IsCurrentBelowVah(grid),
        XGridSignalKind.BelowVal => XGridStats.IsCurrentBelowVal(grid),
        XGridSignalKind.BelowVwap => XGridStats.IsCurrentBelowVwap(grid),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static string Label(XGridSignalKind kind) => kind switch
    {
        XGridSignalKind.RankTopPercentTotal => "rank-top-total",
        XGridSignalKind.RankTopPercentUp => "rank-top-up",
        XGridSignalKind.RankTopPercentDown => "rank-top-down",
        XGridSignalKind.AtPoc => "at-poc",
        XGridSignalKind.AtVah => "at-vah",
        XGridSignalKind.AtVal => "at-val",
        XGridSignalKind.AtVwap => "at-vwap",
        XGridSignalKind.AbovePoc => "above-poc",
        XGridSignalKind.AboveVah => "above-vah",
        XGridSignalKind.AboveVal => "above-val",
        XGridSignalKind.AboveVwap => "above-vwap",
        XGridSignalKind.BelowPoc => "below-poc",
        XGridSignalKind.BelowVah => "below-vah",
        XGridSignalKind.BelowVal => "below-val",
        XGridSignalKind.BelowVwap => "below-vwap",
        _ => kind.ToString(),
    };

    // Compact display text for UI chips (e.g. TrainingPreview's Source column) -- distinct
    // from Label, which is the machine-readable feature name matched against RlInputFeature.
    public static string ShortLabel(XGridSignalKind kind) => kind switch
    {
        XGridSignalKind.RankTopPercentTotal => "Top% Total",
        XGridSignalKind.RankTopPercentUp => "Top% Up",
        XGridSignalKind.RankTopPercentDown => "Top% Down",
        XGridSignalKind.AtPoc => "At POC",
        XGridSignalKind.AtVah => "At VAH",
        XGridSignalKind.AtVal => "At VAL",
        XGridSignalKind.AtVwap => "At VWAP",
        XGridSignalKind.AbovePoc => "Above POC",
        XGridSignalKind.AboveVah => "Above VAH",
        XGridSignalKind.AboveVal => "Above VAL",
        XGridSignalKind.AboveVwap => "Above VWAP",
        XGridSignalKind.BelowPoc => "Below POC",
        XGridSignalKind.BelowVah => "Below VAH",
        XGridSignalKind.BelowVal => "Below VAL",
        XGridSignalKind.BelowVwap => "Below VWAP",
        _ => kind.ToString(),
    };
}
