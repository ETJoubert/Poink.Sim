using System;
using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Charts;

// One row x column intersection of XGrid. Value is the metric XGrid's cell colour is
// driven by -- Count stands in for it until X (the real signal) is decided; Count/
// UpCount/DownCount ride along so a future X can be computed without reshaping the grid.
// Public (not internal) since XGrid.razor.cs's [Parameter] Grid exposes it -- same reason
// TickVolumeProfileChart's WeightedPriceSample is public rather than internal.
public readonly record struct XGridCell(int Count, int UpCount, int DownCount, double Value);

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
// there isn't enough data for that particular level, same as those.
public readonly record struct XGridResult(
    IReadOnlyList<double> RowLow, IReadOnlyList<double> RowHigh, IReadOnlyList<XGridColumn> Columns, double MaxValue,
    IReadOnlyList<double> SweepLeftToRight, IReadOnlyList<double> SweepRightToLeft, IReadOnlyList<double> SweepSum,
    double MaxSweepValue,
    XGridLevel? Vah, XGridLevel? Val, XGridLevel? Vwap, XGridLevel? Average, XGridLevel? Median);

// Footprint-style price/bar matrix -- rows are price buckets (shared across every column,
// same bucketing approach as VolumeProfileStats), columns are the bars inside ProfilerPage's
// Volume Range selection, grouped down to colCount columns when the selection holds more
// bars than that (each column then merges its group's histograms, same "sum the per-bucket
// counts" reduction BarPriceHistogram.Merge already does across a whole window). Kept
// separate from XGrid.razor so the component stays render-only (see DrawableOhlcvChart.
// README's three-layer split for the same reasoning applied to draw tools).
internal static class XGridStats
{
    public static XGridResult? Compute(
        IReadOnlyList<HlocAvdtBar> bars, IReadOnlyList<BarPriceHistogram> histograms,
        IReadOnlyList<WeightedPriceSample> samples, DateTime rangeStart, DateTime rangeEnd, int rowCount, int colCount)
    {
        var indices = new List<int>();
        for (var i = 0; i < bars.Count; i++)
            if (bars[i].OpenTime >= rangeStart && bars[i].OpenTime <= rangeEnd)
                indices.Add(i);
        if (indices.Count == 0) return null;

        rowCount = Math.Max(1, rowCount);
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

        var size = (max - min) / rowCount;
        var rowLow = new List<double>(rowCount);
        var rowHigh = new List<double>(rowCount);
        for (var r = 0; r < rowCount; r++)
        {
            rowHigh.Add(max - size * r);
            rowLow.Add(max - size * (r + 1));
        }

        var columns = new List<XGridColumn>(colCount);
        var maxValue = 0.0;
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
            for (var gi = groupStart; gi < groupEnd; gi++)
            {
                var hist = histograms[indices[gi]];
                foreach ((long key, PriceBucketCounts c) in hist.Buckets)
                {
                    double price = key * hist.BucketWidth;
                    // 0 = top row (highest price), rowCount - 1 = bottom row (lowest price).
                    int row = Math.Clamp((int)((max - price) / size), 0, rowCount - 1);
                    counts[row] += c.Count;
                    upCounts[row] += c.UpCount;
                    downCounts[row] += c.DownCount;
                }
            }

            var cells = new List<XGridCell>(rowCount);
            for (var r = 0; r < rowCount; r++)
            {
                double value = counts[r];
                if (value > maxValue) maxValue = value;
                cells.Add(new XGridCell(counts[r], upCounts[r], downCounts[r], value));
            }
            columns.Add(new XGridColumn(bars[indices[groupStart]].OpenTime, cells));
        }

        var (sweepLtr, sweepRtl, sweepSum, maxSweepValue) = ComputeSweeps(bars, histograms, indices, min, max, size, rowCount);

        XGridLevel? ToLevel(double? price) => price is { } p
            ? new XGridLevel(p, Math.Clamp((int)((max - p) / size), 0, rowCount - 1))
            : null;

        var profileLevels = VolumeProfileStats.Compute(samples);
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

        return new XGridResult(
            rowLow, rowHigh, columns, maxValue, sweepLtr, sweepRtl, sweepSum, maxSweepValue,
            vah, val, vwap, average, median);
    }

    // Walks every possible position of the Volume Range's near border, one raw bar at a
    // time (independent of colCount's column grouping -- a dragged border moves bar by
    // bar, not group by group), tallying which row each resulting window's POC lands on.
    // L->R pins the right edge at indices[^1] and sweeps the left edge indices[0]..indices[^1]
    // (window [i, ^1]); R->L pins the left edge at indices[0] and sweeps the right edge the
    // same span (window [0, i]). Prefix/suffix sums keep this O(bars x rowCount) instead of
    // re-summing each window from scratch.
    private static (double[] Ltr, double[] Rtl, double[] Sum, double MaxSum) ComputeSweeps(
        IReadOnlyList<HlocAvdtBar> bars, IReadOnlyList<BarPriceHistogram> histograms, List<int> indices,
        double min, double max, double size, int rowCount)
    {
        var barRowCounts = new double[indices.Count][];
        for (var i = 0; i < indices.Count; i++)
        {
            var hist = histograms[indices[i]];
            var rc = new double[rowCount];
            foreach ((long key, PriceBucketCounts c) in hist.Buckets)
            {
                double price = key * hist.BucketWidth;
                int row = Math.Clamp((int)((max - price) / size), 0, rowCount - 1);
                rc[row] += c.Count;
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
}
