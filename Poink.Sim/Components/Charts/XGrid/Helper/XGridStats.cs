using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Charts;

/// <summary>
/// One row x column intersection of XGrid. Value is the metric XGrid's cell colour is driven by; Count/
/// UpCount/DownCount ride along so a future replacement metric can be computed without reshaping the grid.
/// Public (not internal) since XGrid.razor.cs's [Parameter] Grid exposes it.
/// </summary>
public readonly record struct XGridCell(
	int Count,
	int UpCount,
	int DownCount,
	double UpDistance,
	double DownDistance,
	double Value
);

// One bar's column -- Cells is row-ordered high-to-low, matching XGridResult's RowLow/RowHigh.
public readonly record struct XGridColumn(DateTime OpenTime, IReadOnlyList<XGridCell> Cells);

// A single reference price pinned to whichever XGrid row contains it -- Row indexes
// XGridResult.RowLow/RowHigh the same way XGridColumn.Cells does.
public readonly record struct XGridLevel(double Price, int Row);

/// <summary>
/// Full grid output. SweepLeftToRight/SweepRightToLeft/SweepSum count, per row, how many Volume-Range-sweep
/// steps landed their window's POC there (see <see cref="XGridStats.Compute"/>). Vah/Val/Vwap/Average/Median/
/// RegressionLine/TotalPeak/UpPeak/DownPeak are null when there isn't enough data for that level; TotalPeak/UpPeak/DownPeak
/// are computed independently from Poc and can land on a different row. NetByColumn is the running
/// cumulative SBuy - SSell, one point per column.
/// </summary>
public readonly record struct XGridResult(
	IReadOnlyList<double> RowLow,
	IReadOnlyList<double> RowHigh,
	IReadOnlyList<XGridColumn> Columns,
	double MaxValue,
	int MaxUpCount,
	int MaxDownCount,
	int MaxAbsDelta,
	double MaxDistanceValue,
	double MaxAbsDeltaDistance,
	double MaxUpDistance,
	double MaxDownDistance,
	double SBuy,
	double SSell,
	IReadOnlyList<double> NetByColumn,
	IReadOnlyList<double> SweepLeftToRight,
	IReadOnlyList<double> SweepRightToLeft,
	IReadOnlyList<double> SweepSum,
	double MaxSweepValue,
	XGridLevel? Poc,
	XGridLevel? Vah,
	XGridLevel? Val,
	XGridLevel? Vwap,
	XGridLevel? Average,
	XGridLevel? Median,
	XGridLevel? RegressionLine,
	XGridLevel? TotalPeak,
	XGridLevel? UpPeak,
	XGridLevel? DownPeak,
	XGridLevel? Current
);

// Up/Down isolate one side's ticks from Total; Delta ranks the signed up-minus-down
// imbalance, ascending from most sell-heavy to most buy-heavy.
internal enum XGridSpectrumMetric
{
	Total,
	Up,
	Down,
	Delta,
}

/// <summary>
/// How a Percent value becomes a cutoff -- Rank highlights that share of the grid's nonzero cells;
/// Max highlights every cell at least that share of the metric's peak. Public (not internal) since
/// XGridSpectrumSettings.HighlightMode exposes it, same rationale as XGridResult.
/// </summary>
public enum XGridHighlightMode
{
	Rank,
	Max,
}

/// <summary>
/// Fixed clears an absolute row-count prominence; Adaptive keeps the top Percent% of candidates
/// found in the current view. Public (not internal) since XGridPeakOverlaySettings.Mode exposes it,
/// same rationale as XGridResult.
/// </summary>
public enum XGridPeakMode
{
	Fixed,
	Adaptive,
}

// A column's extreme cell for peak-finding -- Value is the row, negated for highs, so a
// FindPeaks maximum is always a price extreme.
internal readonly record struct XGridColumnExtreme(int Col, int Row, double Value);

// Peak-finding cutoff config for one direction (low or high); see XGridStats.FindPeaks.
internal readonly record struct XGridPeakSettings(
	XGridPeakMode Mode,
	double MinProminence,
	double AdaptivePercent,
	int MinDistance
);

// Footprint-style price/bar matrix -- rows are shared price buckets, columns are ProfilerPage's Volume
// Range bars grouped down to colCount. Kept separate from XGrid.razor so the component stays render-only.
internal static class XGridStats
{
	// Safety cap on rows derived from a tiny rowHeight over a wide range -- rows past this
	// are dropped from the low end rather than shrinking rowHeight to fit.
	private const int MaxRows = 1000;

	public static XGridResult? Compute(
		IReadOnlyList<HlocAvdtBar> bars,
		IReadOnlyList<BarPriceHistogram> histograms,
		IReadOnlyList<WeightedPriceSample> samples,
		DateTime rangeStart,
		DateTime rangeEnd,
		double rowHeight,
		int colCount
	)
	{
		var indices = new List<int>();
		for (var i = 0; i < bars.Count; i++)
			if (bars[i].OpenTime >= rangeStart && bars[i].OpenTime <= rangeEnd)
				indices.Add(i);
		if (indices.Count == 0)
			return null;

		rowHeight = rowHeight > 0 ? rowHeight : double.Epsilon;
		colCount = Math.Clamp(colCount, 1, indices.Count);

		double min = double.MaxValue,
			max = double.MinValue;
		foreach (var i in indices)
		{
			var hist = histograms[i];
			foreach (var (key, _) in hist.Buckets)
			{
				var low = key * hist.BucketWidth;
				if (low < min)
					min = low;
				if (low + hist.BucketWidth > max)
					max = low + hist.BucketWidth;
			}
		}
		if (max <= min)
			return null;

		// Rows snap to absolute multiples of rowHeight so edges stay fixed across renders
		// and windows; row 0 is the top (highest price), rowCount - 1 is the bottom.
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
		var maxUpCount = 0;
		var maxDownCount = 0;
		var maxAbsDelta = 0;
		var maxDistanceValue = 0.0;
		var maxAbsDeltaDistance = 0.0;
		var maxUpDistance = 0.0;
		var maxDownDistance = 0.0;
		var sBuy = 0.0;
		var sSell = 0.0;
		var netByColumn = new List<double>(colCount);
		var rowTotalCounts = new double[rowCount];
		var rowUpCounts = new double[rowCount];
		var rowDownCounts = new double[rowCount];
		for (var g = 0; g < colCount; g++)
		{
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
				if (value > maxValue)
					maxValue = value;
				if (upCounts[r] > maxUpCount)
					maxUpCount = upCounts[r];
				if (downCounts[r] > maxDownCount)
					maxDownCount = downCounts[r];
				var absDelta = Math.Abs(upCounts[r] - downCounts[r]);
				if (absDelta > maxAbsDelta)
					maxAbsDelta = absDelta;
				var distanceValue = upDistances[r] + downDistances[r];
				if (distanceValue > maxDistanceValue)
					maxDistanceValue = distanceValue;
				var absDeltaDistance = Math.Abs(upDistances[r] - downDistances[r]);
				if (absDeltaDistance > maxAbsDeltaDistance)
					maxAbsDeltaDistance = absDeltaDistance;
				if (upDistances[r] > maxUpDistance)
					maxUpDistance = upDistances[r];
				if (downDistances[r] > maxDownDistance)
					maxDownDistance = downDistances[r];
				cells.Add(
					new XGridCell(counts[r], upCounts[r], downCounts[r], upDistances[r], downDistances[r], value)
				);
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

		double totalVolume = 0,
			totalPriceVolume = 0;
		foreach (var i in indices)
		{
			totalPriceVolume += bars[i].Average * bars[i].Volume;
			totalVolume += bars[i].Volume;
		}
		var vwap = ToLevel(totalVolume > 0 ? (double?)(totalPriceVolume / totalVolume) : null);
		var current = ToLevel(bars[indices[^1]].Close);
		var regressionLine = ToLevel(ComputeRegressionCurrent(bars, indices));

		XGridLevel? PeakLevel(double[] rowSums)
		{
			var best = -1;
			var bestValue = 0.0;
			for (var r = 0; r < rowSums.Length; r++)
				if (rowSums[r] > bestValue)
				{
					bestValue = rowSums[r];
					best = r;
				}
			return best < 0 ? null : new XGridLevel((rowLow[best] + rowHigh[best]) / 2, best);
		}
		var totalPeak = PeakLevel(rowTotalCounts);
		var upPeak = PeakLevel(rowUpCounts);
		var downPeak = PeakLevel(rowDownCounts);

		return new XGridResult(
			rowLow,
			rowHigh,
			columns,
			maxValue,
			maxUpCount,
			maxDownCount,
			maxAbsDelta,
			maxDistanceValue,
			maxAbsDeltaDistance,
			maxUpDistance,
			maxDownDistance,
			sBuy,
			sSell,
			netByColumn,
			sweepLtr,
			sweepRtl,
			sweepSum,
			maxSweepValue,
			poc,
			vah,
			val,
			vwap,
			average,
			median,
			regressionLine,
			totalPeak,
			upPeak,
			downPeak,
			current
		);
	}

	// Ordinary-least-squares fit of Close against bar position, evaluated at the last (current) bar.
	private static double? ComputeRegressionCurrent(IReadOnlyList<HlocAvdtBar> bars, List<int> indices)
	{
		int n = indices.Count;
		if (n < 2)
			return null;

		double sumX = 0,
			sumY = 0,
			sumXY = 0,
			sumXX = 0;
		for (var x = 0; x < n; x++)
		{
			double y = bars[indices[x]].Close;
			sumX += x;
			sumY += y;
			sumXY += x * y;
			sumXX += (double)x * x;
		}

		double denominator = n * sumXX - sumX * sumX;
		if (denominator == 0)
			return null;

		double slope = (n * sumXY - sumX * sumY) / denominator;
		double intercept = (sumY - slope * sumX) / n;
		return slope * (n - 1) + intercept;
	}

	// Walks every position of the Volume Range's near border, one raw bar at a time, tallying
	// which row each resulting window's POC lands on; prefix/suffix sums keep this O(bars x rowCount).
	private static (double[] Ltr, double[] Rtl, double[] Sum, double MaxSum) ComputeSweeps(
		IReadOnlyList<BarPriceHistogram> histograms,
		List<int> indices,
		int rowCount,
		Func<double, int> rowOf
	)
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
			for (var r = 0; r < rowCount; r++)
				suffix[r] += barRowCounts[i][r];
			sweepLtr[ArgMax(suffix)]++;
		}

		var sweepRtl = new double[rowCount];
		var prefix = new double[rowCount];
		for (var i = 0; i < indices.Count; i++)
		{
			for (var r = 0; r < rowCount; r++)
				prefix[r] += barRowCounts[i][r];
			sweepRtl[ArgMax(prefix)]++;
		}

		var sweepSum = new double[rowCount];
		var maxSweepValue = 0.0;
		for (var r = 0; r < rowCount; r++)
		{
			sweepSum[r] = sweepLtr[r] + sweepRtl[r];
			if (sweepSum[r] > maxSweepValue)
				maxSweepValue = sweepSum[r];
		}

		return (sweepLtr, sweepRtl, sweepSum, maxSweepValue);
	}

	private static int ArgMax(double[] values)
	{
		var best = 0;
		for (var i = 1; i < values.Length; i++)
			if (values[i] > values[best])
				best = i;
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

	// Compares actual price, not row -- finer-grained than IsCurrentAtLevel's row match,
	// since a row is a bucket that can span many price levels.
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

	public static bool IsCurrentAboveRegressionLine(XGridResult grid) => IsCurrentAboveLevel(grid, grid.RegressionLine);

	public static bool IsCurrentBelowRegressionLine(XGridResult grid) => IsCurrentBelowLevel(grid, grid.RegressionLine);

	// Level-vs-level comparison -- same price-for-above/below, row-for-at split as
	// IsCurrentAboveLevel/IsCurrentAtLevel, between two computed levels.
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

	public static double CellValue(
		XGridResult grid,
		(int Row, int Col) cell,
		XGridSpectrumMetric metric,
		bool distanceWeighted = false
	)
	{
		XGridCell c = grid.Columns[cell.Col].Cells[cell.Row];
		if (distanceWeighted)
			return metric switch
			{
				XGridSpectrumMetric.Up => c.UpDistance,
				XGridSpectrumMetric.Down => c.DownDistance,
				XGridSpectrumMetric.Delta => c.UpDistance - c.DownDistance,
				_ => c.UpDistance + c.DownDistance,
			};
		return metric switch
		{
			XGridSpectrumMetric.Up => c.UpCount,
			XGridSpectrumMetric.Down => c.DownCount,
			XGridSpectrumMetric.Delta => c.UpCount - c.DownCount,
			_ => c.Value,
		};
	}

	// The grid-wide ceiling for a metric -- Delta uses the signed metric's absolute ceiling
	// since both its directions are judged against the same peak imbalance.
	public static double MaxCellValue(XGridResult grid, XGridSpectrumMetric metric, bool distanceWeighted = false)
	{
		if (distanceWeighted)
			return metric switch
			{
				XGridSpectrumMetric.Up => grid.MaxUpDistance,
				XGridSpectrumMetric.Down => grid.MaxDownDistance,
				XGridSpectrumMetric.Delta => grid.MaxAbsDeltaDistance,
				_ => grid.MaxDistanceValue,
			};
		return metric switch
		{
			XGridSpectrumMetric.Up => grid.MaxUpCount,
			XGridSpectrumMetric.Down => grid.MaxDownCount,
			XGridSpectrumMetric.Delta => grid.MaxAbsDelta,
			_ => grid.MaxValue,
		};
	}

	/// <summary>
	/// Every (row, col) cell with a nonzero metric value, ordered least to most by it. Zero cells are
	/// excluded rather than sorted to the front -- a footprint grid is mostly empty, so including them
	/// would flatten the curve into a long zero run.
	/// </summary>
	public static (int Row, int Col)[] CellOrder(
		XGridResult grid,
		XGridSpectrumMetric metric,
		bool distanceWeighted = false
	)
	{
		int rows = grid.RowLow.Count,
			cols = grid.Columns.Count;
		var cells = new List<(int Row, int Col)>(rows * cols);
		for (var c = 0; c < cols; c++)
		for (var r = 0; r < rows; r++)
			if (CellValue(grid, (r, c), metric, distanceWeighted) != 0)
				cells.Add((r, c));

		var order = cells.ToArray();
		Array.Sort(
			order,
			(a, b) => CellValue(grid, a, metric, distanceWeighted).CompareTo(CellValue(grid, b, metric, distanceWeighted))
		);
		return order;
	}

	// How many of the n nonzero cells fall within the top Percent% by value.
	public static int TopCount(int n, double percent) =>
		Math.Clamp((int)Math.Round(n * Math.Clamp(percent, 0, 100) / 100.0, MidpointRounding.AwayFromZero), 0, n);

	// Max mode's cutoff: the top Percent% of the value range below max, so Percent behaves the
	// same as Rank mode's -- 10 means "the top 10%", not "at least 10% of the peak".
	public static double MaxModeThreshold(double max, double percent) =>
		max * (1 - Math.Clamp(percent, 0, 100) / 100.0);

	// Delta's Max-mode ceiling for one side's sign only, so the larger side's imbalance can't
	// suppress highlights on the smaller side via the shared grid-wide MaxAbsDelta.
	public static double MaxAbsDeltaOnSide(XGridResult grid, bool positive, bool distanceWeighted = false)
	{
		int rows = grid.RowLow.Count,
			cols = grid.Columns.Count;
		double max = 0;
		for (var c = 0; c < cols; c++)
		for (var r = 0; r < rows; r++)
		{
			double value = CellValue(grid, (r, c), XGridSpectrumMetric.Delta, distanceWeighted);
			if (positive ? value <= 0 : value >= 0)
				continue;
			double abs = positive ? value : -value;
			if (abs > max)
				max = abs;
		}
		return max;
	}

	/// <summary>
	/// True when the current (latest) cell's metric value ranks inside the top percent% of every
	/// nonzero cell on the grid (Rank mode), or is at least percent% of the metric's grid-wide
	/// peak (Max mode). A zero-value current cell never qualifies under either mode.
	/// </summary>
	public static bool IsCurrentInTopPercent(
		XGridResult grid,
		XGridSpectrumMetric metric,
		double percent,
		XGridHighlightMode mode = XGridHighlightMode.Rank,
		bool distanceWeighted = false
	)
	{
		if (CurrentCell(grid) is not { } cell)
			return false;

		if (mode == XGridHighlightMode.Max)
		{
			double value = CellValue(grid, cell, metric, distanceWeighted);
			double max = MaxCellValue(grid, metric, distanceWeighted);
			return value > 0 && max > 0 && value >= MaxModeThreshold(max, percent);
		}

		var order = CellOrder(grid, metric, distanceWeighted);
		int rank = Array.IndexOf(order, cell);
		if (rank < 0)
			return false;

		int n = order.Length;
		return rank >= n - TopCount(n, percent);
	}

	/// <summary>
	/// True when the current (latest) cell's price ranks inside the top percent% of the signed
	/// Delta order on the requested side -- positive slices the ascending tail (most buy-heavy),
	/// negative the head (most sell-heavy) in Rank mode, or is at least percent% of the grid's
	/// peak imbalance in that direction in Max mode -- and its value actually carries that sign.
	/// </summary>
	public static bool IsCurrentInDeltaPercent(
		XGridResult grid,
		double percent,
		bool positive,
		XGridHighlightMode mode = XGridHighlightMode.Rank,
		bool distanceWeighted = false
	)
	{
		if (CurrentCell(grid) is not { } cell)
			return false;

		if (mode == XGridHighlightMode.Max)
		{
			double deltaMax = CellValue(grid, cell, XGridSpectrumMetric.Delta, distanceWeighted);
			double max = MaxAbsDeltaOnSide(grid, positive, distanceWeighted);
			if (max <= 0)
				return false;
			double threshold = MaxModeThreshold(max, percent);
			return positive ? deltaMax >= threshold && deltaMax > 0 : -deltaMax >= threshold && deltaMax < 0;
		}

		var order = CellOrder(grid, XGridSpectrumMetric.Delta, distanceWeighted);
		int rank = Array.IndexOf(order, cell);
		if (rank < 0)
			return false;

		int n = order.Length;
		int count = TopCount(n, percent);
		bool inSlice = positive ? rank >= n - count : rank < count;
		if (!inSlice)
			return false;

		double deltaRank = CellValue(grid, cell, XGridSpectrumMetric.Delta, distanceWeighted);
		return positive ? deltaRank > 0 : deltaRank < 0;
	}

	// Swing-low/-high ranking: local maxima of a per-column extreme-price series (see FindPeaks).
	public static IReadOnlyList<(int Row, int Col)> LowPointCells(XGridResult grid, XGridPeakSettings settings) =>
		ExtremePoints(grid, settings, lowest: true);

	public static IReadOnlyList<(int Row, int Col)> HighPointCells(XGridResult grid, XGridPeakSettings settings) =>
		ExtremePoints(grid, settings, lowest: false);

	// The most recent (rightmost-column) swing point as a level, so it can drive the same
	// At/Above/Below comparisons Poc/Vah/Val/Vwap already use. Null when none survive settings.
	public static XGridLevel? LatestLowLevel(XGridResult grid, XGridPeakSettings settings) =>
		LatestLevel(grid, LowPointCells(grid, settings));

	public static XGridLevel? LatestHighLevel(XGridResult grid, XGridPeakSettings settings) =>
		LatestLevel(grid, HighPointCells(grid, settings));

	private static XGridLevel? LatestLevel(XGridResult grid, IReadOnlyList<(int Row, int Col)> points)
	{
		if (points.Count == 0)
			return null;
		var latest = points[0];
		for (var i = 1; i < points.Count; i++)
			if (points[i].Col > latest.Col)
				latest = points[i];
		return new XGridLevel((grid.RowLow[latest.Row] + grid.RowHigh[latest.Row]) / 2, latest.Row);
	}

	public static bool IsCurrentAtLowLevel(XGridResult grid, XGridPeakSettings settings) =>
		IsCurrentAtLevel(grid, LatestLowLevel(grid, settings));

	public static bool IsCurrentAboveLowLevel(XGridResult grid, XGridPeakSettings settings) =>
		IsCurrentAboveLevel(grid, LatestLowLevel(grid, settings));

	public static bool IsCurrentBelowLowLevel(XGridResult grid, XGridPeakSettings settings) =>
		IsCurrentBelowLevel(grid, LatestLowLevel(grid, settings));

	public static bool IsCurrentAtHighLevel(XGridResult grid, XGridPeakSettings settings) =>
		IsCurrentAtLevel(grid, LatestHighLevel(grid, settings));

	public static bool IsCurrentAboveHighLevel(XGridResult grid, XGridPeakSettings settings) =>
		IsCurrentAboveLevel(grid, LatestHighLevel(grid, settings));

	public static bool IsCurrentBelowHighLevel(XGridResult grid, XGridPeakSettings settings) =>
		IsCurrentBelowLevel(grid, LatestHighLevel(grid, settings));

	private static IReadOnlyList<(int Row, int Col)> ExtremePoints(
		XGridResult grid,
		XGridPeakSettings settings,
		bool lowest
	)
	{
		var series = new List<XGridColumnExtreme>();
		for (var c = 0; c < grid.Columns.Count; c++)
			if (ColumnExtremeRow(grid.Columns[c], lowest) is { } row)
				series.Add(new XGridColumnExtreme(c, row, lowest ? row : -row));

		var peaks = FindPeaks(series, settings);
		var points = new List<(int Row, int Col)>(peaks.Count);
		foreach (var peak in peaks)
			points.Add((peak.Row, peak.Col));
		return points;
	}

	// Bottom-most (lowest) or top-most (highest) nonzero row in the column; null if empty.
	private static int? ColumnExtremeRow(XGridColumn column, bool lowest)
	{
		var cells = column.Cells;
		int count = cells.Count;
		for (var i = 0; i < count; i++)
		{
			int r = lowest ? count - 1 - i : i;
			if (cells[r].Count > 0)
				return r;
		}
		return null;
	}

	// Local maxima of series.Value, plateau-aware: a flat run strictly higher than the value
	// before and after it counts as one peak (its midpoint), not zero.
	private static List<(XGridColumnExtreme Point, double Prominence)> PeakCandidates(
		IReadOnlyList<XGridColumnExtreme> series
	)
	{
		var candidates = new List<(XGridColumnExtreme Point, double Prominence)>();
		int n = series.Count;
		var i = 1;
		while (i < n - 1)
		{
			double v = series[i].Value;
			if (series[i - 1].Value >= v)
			{
				i++;
				continue;
			}

			int end = i;
			while (end + 1 < n && series[end + 1].Value == v)
				end++;

			if (end < n - 1 && series[end + 1].Value < v)
			{
				double leftMin = series[i - 1].Value;
				for (var j = i - 1; j >= 0 && series[j].Value <= v; j--)
					leftMin = Math.Min(leftMin, series[j].Value);

				double rightMin = series[end + 1].Value;
				for (var j = end + 1; j < n && series[j].Value <= v; j++)
					rightMin = Math.Min(rightMin, series[j].Value);

				candidates.Add((series[(i + end) / 2], v - Math.Max(leftMin, rightMin)));
			}

			i = end + 1;
		}
		return candidates;
	}

	// Filters PeakCandidates by settings.Mode, then keeps strongest-prominence first, skipping
	// any candidate within MinDistance columns of one already kept.
	private static List<XGridColumnExtreme> FindPeaks(
		IReadOnlyList<XGridColumnExtreme> series,
		XGridPeakSettings settings
	)
	{
		var candidates = PeakCandidates(series);

		List<(XGridColumnExtreme Point, double Prominence)> filtered;
		if (settings.Mode == XGridPeakMode.Adaptive)
		{
			candidates.Sort((a, b) => a.Prominence.CompareTo(b.Prominence));
			int count = TopCount(candidates.Count, settings.AdaptivePercent);
			filtered = candidates.GetRange(candidates.Count - count, count);
		}
		else
		{
			filtered = new List<(XGridColumnExtreme Point, double Prominence)>();
			foreach (var c in candidates)
				if (c.Prominence >= settings.MinProminence)
					filtered.Add(c);
		}

		filtered.Sort((a, b) => b.Prominence.CompareTo(a.Prominence));
		int minDistance = Math.Max(1, settings.MinDistance);
		var kept = new List<XGridColumnExtreme>();
		foreach (var (point, _) in filtered)
		{
			var tooClose = false;
			foreach (var k in kept)
				if (Math.Abs(k.Col - point.Col) < minDistance)
				{
					tooClose = true;
					break;
				}
			if (!tooClose)
				kept.Add(point);
		}
		return kept;
	}
}

// A signal is one boolean condition over a grid -- Key is its stable RL feature name/persisted
// toggle identity, Group/Label/Tooltip drive the Brain Builder checkbox UI.
internal delegate bool XGridSignalPredicate(XGridResult grid, double percent);

// Above/In/Below groups a row's related conditions for table-style UI (Rule Engine); a
// row with only one condition (Rank-style) always uses In, by convention.
internal enum XGridSignalDirection
{
	Above,
	In,
	Below,
}

// RowKey/RowLabel/ColorVar/Direction are this condition's place in a display row -- every row
// with the same RowKey (see XGridRuleCatalog) is declared together below, in display order.
internal sealed record XGridSignalDef(
	string Key,
	string RowKey,
	string RowLabel,
	string ColorVar,
	XGridSignalDirection Direction,
	string Group,
	string Label,
	string Tooltip,
	XGridSignalPredicate Predicate
);

// The single source of truth for every XGrid signal -- Rule Engine rows, Brain Builder's
// checkboxes, and Brain IO's readout all project off this list instead of redeclaring it.
internal static class XGridSignalCatalog
{
	// Well-known keys referenced by identity elsewhere in code (mask rules) -- everything else
	// is only ever looked up by iterating All, so it doesn't need a named constant here.
	public static class Keys
	{
		public const string RankTopPercentUp = "rank-top-up";
		public const string RankTopPercentDown = "rank-top-down";
		public const string DeltaUp = "delta-up";
		public const string DeltaDown = "delta-down";
		public const string AtPoc = "at-poc";
		public const string AtVah = "at-vah";
		public const string AtVal = "at-val";
		public const string AtVwap = "at-vwap";
		public const string BelowPoc = "below-poc";
	}

	public static readonly IReadOnlyList<XGridSignalDef> All = new[]
	{
		new XGridSignalDef(
			Key: "rank-top-total",
			RowKey: "rank-total",
			RowLabel: "Top % Total",
			ColorVar: "--color-secondary",
			Direction: XGridSignalDirection.In,
			Group: "Rank",
			Label: "Top % Total",
			Tooltip: "Current cell's total tick count ranks in the top % of every nonzero cell on the grid",
			Predicate: (g, pct) => XGridStats.IsCurrentInTopPercent(g, XGridSpectrumMetric.Total, pct)
		),
		new XGridSignalDef(
			Key: Keys.RankTopPercentUp,
			RowKey: "rank-up",
			RowLabel: "Top % Up",
			ColorVar: "--color-success",
			Direction: XGridSignalDirection.In,
			Group: "Rank",
			Label: "Top % Up",
			Tooltip: "Current cell's up-tick count ranks in the top % of every nonzero cell (Up metric)",
			Predicate: (g, pct) => XGridStats.IsCurrentInTopPercent(g, XGridSpectrumMetric.Up, pct)
		),
		new XGridSignalDef(
			Key: Keys.RankTopPercentDown,
			RowKey: "rank-down",
			RowLabel: "Top % Down",
			ColorVar: "--color-error",
			Direction: XGridSignalDirection.In,
			Group: "Rank",
			Label: "Top % Down",
			Tooltip: "Current cell's down-tick count ranks in the top % of every nonzero cell (Down metric)",
			Predicate: (g, pct) => XGridStats.IsCurrentInTopPercent(g, XGridSpectrumMetric.Down, pct)
		),
		new XGridSignalDef(
			Key: Keys.DeltaUp,
			RowKey: "delta-up",
			RowLabel: "Delta Up",
			ColorVar: "--color-badge-1",
			Direction: XGridSignalDirection.In,
			Group: "Rank",
			Label: "Delta Up",
			Tooltip: "Current price's cell ranks in the top % most buy-heavy cells by up-minus-down delta",
			Predicate: (g, pct) => XGridStats.IsCurrentInDeltaPercent(g, pct, positive: true)
		),
		new XGridSignalDef(
			Key: Keys.DeltaDown,
			RowKey: "delta-down",
			RowLabel: "Delta Down",
			ColorVar: "--color-badge-3",
			Direction: XGridSignalDirection.In,
			Group: "Rank",
			Label: "Delta Down",
			Tooltip: "Current price's cell ranks in the top % most sell-heavy cells by down-minus-up delta",
			Predicate: (g, pct) => XGridStats.IsCurrentInDeltaPercent(g, pct, positive: false)
		),
		new XGridSignalDef(
			Key: "above-poc",
			RowKey: "poc",
			RowLabel: "POC",
			ColorVar: "--color-primary",
			Direction: XGridSignalDirection.Above,
			Group: "Above",
			Label: "Above POC",
			Tooltip: "Current price is above the Point of Control's price",
			Predicate: (g, _) => XGridStats.IsCurrentAbovePoc(g)
		),
		new XGridSignalDef(
			Key: Keys.AtPoc,
			RowKey: "poc",
			RowLabel: "POC",
			ColorVar: "--color-primary",
			Direction: XGridSignalDirection.In,
			Group: "At",
			Label: "At POC",
			Tooltip: "Current price's row matches the Point of Control's row",
			Predicate: (g, _) => XGridStats.IsCurrentAtPoc(g)
		),
		new XGridSignalDef(
			Key: Keys.BelowPoc,
			RowKey: "poc",
			RowLabel: "POC",
			ColorVar: "--color-primary",
			Direction: XGridSignalDirection.Below,
			Group: "Below",
			Label: "Below POC",
			Tooltip: "Current price is below the Point of Control's price",
			Predicate: (g, _) => XGridStats.IsCurrentBelowPoc(g)
		),
		new XGridSignalDef(
			Key: "above-vah",
			RowKey: "vah",
			RowLabel: "VAH",
			ColorVar: "--color-highlight",
			Direction: XGridSignalDirection.Above,
			Group: "Above",
			Label: "Above VAH",
			Tooltip: "Current price is above the Value Area High's price",
			Predicate: (g, _) => XGridStats.IsCurrentAboveVah(g)
		),
		new XGridSignalDef(
			Key: Keys.AtVah,
			RowKey: "vah",
			RowLabel: "VAH",
			ColorVar: "--color-highlight",
			Direction: XGridSignalDirection.In,
			Group: "At",
			Label: "At VAH",
			Tooltip: "Current price's row matches the Value Area High's row",
			Predicate: (g, _) => XGridStats.IsCurrentAtVah(g)
		),
		new XGridSignalDef(
			Key: "below-vah",
			RowKey: "vah",
			RowLabel: "VAH",
			ColorVar: "--color-highlight",
			Direction: XGridSignalDirection.Below,
			Group: "Below",
			Label: "Below VAH",
			Tooltip: "Current price is below the Value Area High's price",
			Predicate: (g, _) => XGridStats.IsCurrentBelowVah(g)
		),
		new XGridSignalDef(
			Key: "above-val",
			RowKey: "val",
			RowLabel: "VAL",
			ColorVar: "--color-highlight",
			Direction: XGridSignalDirection.Above,
			Group: "Above",
			Label: "Above VAL",
			Tooltip: "Current price is above the Value Area Low's price",
			Predicate: (g, _) => XGridStats.IsCurrentAboveVal(g)
		),
		new XGridSignalDef(
			Key: Keys.AtVal,
			RowKey: "val",
			RowLabel: "VAL",
			ColorVar: "--color-highlight",
			Direction: XGridSignalDirection.In,
			Group: "At",
			Label: "At VAL",
			Tooltip: "Current price's row matches the Value Area Low's row",
			Predicate: (g, _) => XGridStats.IsCurrentAtVal(g)
		),
		new XGridSignalDef(
			Key: "below-val",
			RowKey: "val",
			RowLabel: "VAL",
			ColorVar: "--color-highlight",
			Direction: XGridSignalDirection.Below,
			Group: "Below",
			Label: "Below VAL",
			Tooltip: "Current price is below the Value Area Low's price",
			Predicate: (g, _) => XGridStats.IsCurrentBelowVal(g)
		),
		new XGridSignalDef(
			Key: "above-vwap",
			RowKey: "vwap",
			RowLabel: "VWAP",
			ColorVar: "--color-tertiary",
			Direction: XGridSignalDirection.Above,
			Group: "Above",
			Label: "Above VWAP",
			Tooltip: "Current price is above the VWAP's price",
			Predicate: (g, _) => XGridStats.IsCurrentAboveVwap(g)
		),
		new XGridSignalDef(
			Key: Keys.AtVwap,
			RowKey: "vwap",
			RowLabel: "VWAP",
			ColorVar: "--color-tertiary",
			Direction: XGridSignalDirection.In,
			Group: "At",
			Label: "At VWAP",
			Tooltip: "Current price's row matches the VWAP's row",
			Predicate: (g, _) => XGridStats.IsCurrentAtVwap(g)
		),
		new XGridSignalDef(
			Key: "below-vwap",
			RowKey: "vwap",
			RowLabel: "VWAP",
			ColorVar: "--color-tertiary",
			Direction: XGridSignalDirection.Below,
			Group: "Below",
			Label: "Below VWAP",
			Tooltip: "Current price is below the VWAP's price",
			Predicate: (g, _) => XGridStats.IsCurrentBelowVwap(g)
		),
		new XGridSignalDef(
			Key: "above-low",
			RowKey: "low",
			RowLabel: "Low",
			ColorVar: "--color-badge-4",
			Direction: XGridSignalDirection.Above,
			Group: "Above",
			Label: "Above Low",
			Tooltip: "Current price is above the latest swing low's price (top % most prominent, adaptive)",
			Predicate: (g, pct) =>
				XGridStats.IsCurrentAboveLowLevel(g, new XGridPeakSettings(XGridPeakMode.Adaptive, 0, pct, 10))
		),
		new XGridSignalDef(
			Key: "at-low",
			RowKey: "low",
			RowLabel: "Low",
			ColorVar: "--color-badge-4",
			Direction: XGridSignalDirection.In,
			Group: "At",
			Label: "At Low",
			Tooltip: "Current price's row matches the latest swing low's row (top % most prominent, adaptive)",
			Predicate: (g, pct) =>
				XGridStats.IsCurrentAtLowLevel(g, new XGridPeakSettings(XGridPeakMode.Adaptive, 0, pct, 10))
		),
		new XGridSignalDef(
			Key: "below-low",
			RowKey: "low",
			RowLabel: "Low",
			ColorVar: "--color-badge-4",
			Direction: XGridSignalDirection.Below,
			Group: "Below",
			Label: "Below Low",
			Tooltip: "Current price is below the latest swing low's price (top % most prominent, adaptive)",
			Predicate: (g, pct) =>
				XGridStats.IsCurrentBelowLowLevel(g, new XGridPeakSettings(XGridPeakMode.Adaptive, 0, pct, 10))
		),
		new XGridSignalDef(
			Key: "above-high",
			RowKey: "high",
			RowLabel: "High",
			ColorVar: "--color-badge-5",
			Direction: XGridSignalDirection.Above,
			Group: "Above",
			Label: "Above High",
			Tooltip: "Current price is above the latest swing high's price (top % most prominent, adaptive)",
			Predicate: (g, pct) =>
				XGridStats.IsCurrentAboveHighLevel(g, new XGridPeakSettings(XGridPeakMode.Adaptive, 0, pct, 10))
		),
		new XGridSignalDef(
			Key: "at-high",
			RowKey: "high",
			RowLabel: "High",
			ColorVar: "--color-badge-5",
			Direction: XGridSignalDirection.In,
			Group: "At",
			Label: "At High",
			Tooltip: "Current price's row matches the latest swing high's row (top % most prominent, adaptive)",
			Predicate: (g, pct) =>
				XGridStats.IsCurrentAtHighLevel(g, new XGridPeakSettings(XGridPeakMode.Adaptive, 0, pct, 10))
		),
		new XGridSignalDef(
			Key: "below-high",
			RowKey: "high",
			RowLabel: "High",
			ColorVar: "--color-badge-5",
			Direction: XGridSignalDirection.Below,
			Group: "Below",
			Label: "Below High",
			Tooltip: "Current price is below the latest swing high's price (top % most prominent, adaptive)",
			Predicate: (g, pct) =>
				XGridStats.IsCurrentBelowHighLevel(g, new XGridPeakSettings(XGridPeakMode.Adaptive, 0, pct, 10))
		),
		new XGridSignalDef(
			Key: "above-reg",
			RowKey: "reg",
			RowLabel: "Reg",
			ColorVar: "--color-badge-2",
			Direction: XGridSignalDirection.Above,
			Group: "Above",
			Label: "Above Regression",
			Tooltip: "Current price is above the linear regression line fitted over the trailing window",
			Predicate: (g, _) => XGridStats.IsCurrentAboveRegressionLine(g)
		),
		new XGridSignalDef(
			Key: "below-reg",
			RowKey: "reg",
			RowLabel: "Reg",
			ColorVar: "--color-badge-2",
			Direction: XGridSignalDirection.Below,
			Group: "Below",
			Label: "Below Regression",
			Tooltip: "Current price is below the linear regression line fitted over the trailing window",
			Predicate: (g, _) => XGridStats.IsCurrentBelowRegressionLine(g)
		),
		new XGridSignalDef(
			Key: "vwap-above-poc",
			RowKey: "vwap-poc",
			RowLabel: "VWAP/POC",
			ColorVar: "--color-tertiary",
			Direction: XGridSignalDirection.Above,
			Group: "Vs POC",
			Label: "VWAP Above POC",
			Tooltip: "VWAP's price is above the Point of Control's price",
			Predicate: (g, _) => XGridStats.IsVwapAbovePoc(g)
		),
		new XGridSignalDef(
			Key: "vwap-at-poc",
			RowKey: "vwap-poc",
			RowLabel: "VWAP/POC",
			ColorVar: "--color-tertiary",
			Direction: XGridSignalDirection.In,
			Group: "Vs POC",
			Label: "VWAP At POC",
			Tooltip: "VWAP's row matches the Point of Control's row",
			Predicate: (g, _) => XGridStats.IsVwapAtPoc(g)
		),
		new XGridSignalDef(
			Key: "vwap-below-poc",
			RowKey: "vwap-poc",
			RowLabel: "VWAP/POC",
			ColorVar: "--color-tertiary",
			Direction: XGridSignalDirection.Below,
			Group: "Vs POC",
			Label: "VWAP Below POC",
			Tooltip: "VWAP's price is below the Point of Control's price",
			Predicate: (g, _) => XGridStats.IsVwapBelowPoc(g)
		),
		new XGridSignalDef(
			Key: "total-above-poc",
			RowKey: "total-poc",
			RowLabel: "Total/POC",
			ColorVar: "--color-primary",
			Direction: XGridSignalDirection.Above,
			Group: "Vs POC",
			Label: "Total Above POC",
			Tooltip: "The grid's own total tick-count peak row is above the Point of Control's price",
			Predicate: (g, _) => XGridStats.IsTotalAbovePoc(g)
		),
		new XGridSignalDef(
			Key: "total-at-poc",
			RowKey: "total-poc",
			RowLabel: "Total/POC",
			ColorVar: "--color-primary",
			Direction: XGridSignalDirection.In,
			Group: "Vs POC",
			Label: "Total At POC",
			Tooltip: "The grid's own total tick-count peak row matches the Point of Control's row",
			Predicate: (g, _) => XGridStats.IsTotalAtPoc(g)
		),
		new XGridSignalDef(
			Key: "total-below-poc",
			RowKey: "total-poc",
			RowLabel: "Total/POC",
			ColorVar: "--color-primary",
			Direction: XGridSignalDirection.Below,
			Group: "Vs POC",
			Label: "Total Below POC",
			Tooltip: "The grid's own total tick-count peak row is below the Point of Control's price",
			Predicate: (g, _) => XGridStats.IsTotalBelowPoc(g)
		),
		new XGridSignalDef(
			Key: "up-above-poc",
			RowKey: "up-poc",
			RowLabel: "Up/POC",
			ColorVar: "--color-success",
			Direction: XGridSignalDirection.Above,
			Group: "Vs POC",
			Label: "Up Above POC",
			Tooltip: "The grid's own up-tick peak row is above the Point of Control's price",
			Predicate: (g, _) => XGridStats.IsUpAbovePoc(g)
		),
		new XGridSignalDef(
			Key: "up-at-poc",
			RowKey: "up-poc",
			RowLabel: "Up/POC",
			ColorVar: "--color-success",
			Direction: XGridSignalDirection.In,
			Group: "Vs POC",
			Label: "Up At POC",
			Tooltip: "The grid's own up-tick peak row matches the Point of Control's row",
			Predicate: (g, _) => XGridStats.IsUpAtPoc(g)
		),
		new XGridSignalDef(
			Key: "up-below-poc",
			RowKey: "up-poc",
			RowLabel: "Up/POC",
			ColorVar: "--color-success",
			Direction: XGridSignalDirection.Below,
			Group: "Vs POC",
			Label: "Up Below POC",
			Tooltip: "The grid's own up-tick peak row is below the Point of Control's price",
			Predicate: (g, _) => XGridStats.IsUpBelowPoc(g)
		),
		new XGridSignalDef(
			Key: "down-above-poc",
			RowKey: "down-poc",
			RowLabel: "Down/POC",
			ColorVar: "--color-error",
			Direction: XGridSignalDirection.Above,
			Group: "Vs POC",
			Label: "Down Above POC",
			Tooltip: "The grid's own down-tick peak row is above the Point of Control's price",
			Predicate: (g, _) => XGridStats.IsDownAbovePoc(g)
		),
		new XGridSignalDef(
			Key: "down-at-poc",
			RowKey: "down-poc",
			RowLabel: "Down/POC",
			ColorVar: "--color-error",
			Direction: XGridSignalDirection.In,
			Group: "Vs POC",
			Label: "Down At POC",
			Tooltip: "The grid's own down-tick peak row matches the Point of Control's row",
			Predicate: (g, _) => XGridStats.IsDownAtPoc(g)
		),
		new XGridSignalDef(
			Key: "down-below-poc",
			RowKey: "down-poc",
			RowLabel: "Down/POC",
			ColorVar: "--color-error",
			Direction: XGridSignalDirection.Below,
			Group: "Vs POC",
			Label: "Down Below POC",
			Tooltip: "The grid's own down-tick peak row is below the Point of Control's price",
			Predicate: (g, _) => XGridStats.IsDownBelowPoc(g)
		),
	};

	private static readonly Dictionary<string, XGridSignalDef> ByKey = All.ToDictionary(d => d.Key);

	public static XGridSignalDef Get(string key) => ByKey[key];

	public static bool Contains(string key) => ByKey.ContainsKey(key);

	public static bool Value(XGridResult grid, string key, double percent) => Get(key).Predicate(grid, percent);
}
