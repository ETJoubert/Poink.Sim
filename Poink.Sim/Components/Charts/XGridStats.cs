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
/// TotalPeak/UpPeak/DownPeak are null when there isn't enough data for that level; TotalPeak/UpPeak/DownPeak
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

// How a Percent value is turned into a cutoff -- Rank always highlights that share of the
// grid's nonzero cells; Max highlights every cell at least that share of the metric's peak.
internal enum XGridHighlightMode
{
	Rank,
	Max,
}

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
			totalPeak,
			upPeak,
			downPeak,
			current
		);
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

	public static double CellValue(XGridResult grid, (int Row, int Col) cell, XGridSpectrumMetric metric)
	{
		XGridCell c = grid.Columns[cell.Col].Cells[cell.Row];
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
	public static double MaxCellValue(XGridResult grid, XGridSpectrumMetric metric) =>
		metric switch
		{
			XGridSpectrumMetric.Up => grid.MaxUpCount,
			XGridSpectrumMetric.Down => grid.MaxDownCount,
			XGridSpectrumMetric.Delta => grid.MaxAbsDelta,
			_ => grid.MaxValue,
		};

	/// <summary>
	/// Every (row, col) cell with a nonzero metric value, ordered least to most by it. Zero cells are
	/// excluded rather than sorted to the front -- a footprint grid is mostly empty, so including them
	/// would flatten the curve into a long zero run.
	/// </summary>
	public static (int Row, int Col)[] CellOrder(XGridResult grid, XGridSpectrumMetric metric)
	{
		int rows = grid.RowLow.Count,
			cols = grid.Columns.Count;
		var cells = new List<(int Row, int Col)>(rows * cols);
		for (var c = 0; c < cols; c++)
		for (var r = 0; r < rows; r++)
			if (CellValue(grid, (r, c), metric) != 0)
				cells.Add((r, c));

		var order = cells.ToArray();
		Array.Sort(order, (a, b) => CellValue(grid, a, metric).CompareTo(CellValue(grid, b, metric)));
		return order;
	}

	// How many of the n nonzero cells fall within the top Percent% by value.
	public static int TopCount(int n, double percent) =>
		Math.Clamp((int)Math.Round(n * Math.Clamp(percent, 0, 100) / 100.0, MidpointRounding.AwayFromZero), 0, n);

	// Max mode's cutoff: the top Percent% of the value range below max, so Percent behaves the
	// same as Rank mode's -- 10 means "the top 10%", not "at least 10% of the peak".
	public static double MaxModeThreshold(double max, double percent) =>
		max * (1 - Math.Clamp(percent, 0, 100) / 100.0);

	/// <summary>
	/// True when the current (latest) cell's metric value ranks inside the top percent% of every
	/// nonzero cell on the grid (Rank mode), or is at least percent% of the metric's grid-wide
	/// peak (Max mode). A zero-value current cell never qualifies under either mode.
	/// </summary>
	public static bool IsCurrentInTopPercent(
		XGridResult grid,
		XGridSpectrumMetric metric,
		double percent,
		XGridHighlightMode mode = XGridHighlightMode.Rank
	)
	{
		if (CurrentCell(grid) is not { } cell)
			return false;

		if (mode == XGridHighlightMode.Max)
		{
			double value = CellValue(grid, cell, metric);
			double max = MaxCellValue(grid, metric);
			return value > 0 && max > 0 && value >= MaxModeThreshold(max, percent);
		}

		var order = CellOrder(grid, metric);
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
		XGridHighlightMode mode = XGridHighlightMode.Rank
	)
	{
		if (CurrentCell(grid) is not { } cell)
			return false;

		if (mode == XGridHighlightMode.Max)
		{
			double deltaMax = CellValue(grid, cell, XGridSpectrumMetric.Delta);
			double max = grid.MaxAbsDelta;
			if (max <= 0)
				return false;
			double threshold = MaxModeThreshold(max, percent);
			return positive ? deltaMax >= threshold && deltaMax > 0 : -deltaMax >= threshold && deltaMax < 0;
		}

		var order = CellOrder(grid, XGridSpectrumMetric.Delta);
		int rank = Array.IndexOf(order, cell);
		if (rank < 0)
			return false;

		int n = order.Length;
		int count = TopCount(n, percent);
		bool inSlice = positive ? rank >= n - count : rank < count;
		if (!inSlice)
			return false;

		double deltaRank = CellValue(grid, cell, XGridSpectrumMetric.Delta);
		return positive ? deltaRank > 0 : deltaRank < 0;
	}
}

// A signal is one boolean condition over a grid -- Key is its stable RL feature name/persisted
// toggle identity, Group/Label/Tooltip drive the Brain Builder checkbox UI.
internal delegate bool XGridSignalPredicate(XGridResult grid, double percent);

internal sealed record XGridSignalDef(
	string Key,
	string Group,
	string Label,
	string Tooltip,
	XGridSignalPredicate Predicate
);

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
		public const string BelowPoc = "below-poc";
	}

	public static readonly IReadOnlyList<XGridSignalDef> All = new[]
	{
		new XGridSignalDef(
			"rank-top-total",
			"Rank",
			"Top % Total",
			"Current cell's total tick count ranks in the top % of every nonzero cell on the grid",
			(g, pct) => XGridStats.IsCurrentInTopPercent(g, XGridSpectrumMetric.Total, pct)
		),
		new XGridSignalDef(
			Keys.RankTopPercentUp,
			"Rank",
			"Top % Up",
			"Current cell's up-tick count ranks in the top % of every nonzero cell (Up metric)",
			(g, pct) => XGridStats.IsCurrentInTopPercent(g, XGridSpectrumMetric.Up, pct)
		),
		new XGridSignalDef(
			Keys.RankTopPercentDown,
			"Rank",
			"Top % Down",
			"Current cell's down-tick count ranks in the top % of every nonzero cell (Down metric)",
			(g, pct) => XGridStats.IsCurrentInTopPercent(g, XGridSpectrumMetric.Down, pct)
		),
		new XGridSignalDef(
			Keys.AtPoc,
			"At",
			"At POC",
			"Current price's row matches the Point of Control's row",
			(g, _) => XGridStats.IsCurrentAtPoc(g)
		),
		new XGridSignalDef(
			Keys.AtVah,
			"At",
			"At VAH",
			"Current price's row matches the Value Area High's row",
			(g, _) => XGridStats.IsCurrentAtVah(g)
		),
		new XGridSignalDef(
			Keys.AtVal,
			"At",
			"At VAL",
			"Current price's row matches the Value Area Low's row",
			(g, _) => XGridStats.IsCurrentAtVal(g)
		),
		new XGridSignalDef(
			Keys.AtVwap,
			"At",
			"At VWAP",
			"Current price's row matches the VWAP's row",
			(g, _) => XGridStats.IsCurrentAtVwap(g)
		),
		new XGridSignalDef(
			"above-poc",
			"Above",
			"Above POC",
			"Current price is above the Point of Control's price",
			(g, _) => XGridStats.IsCurrentAbovePoc(g)
		),
		new XGridSignalDef(
			"above-vah",
			"Above",
			"Above VAH",
			"Current price is above the Value Area High's price",
			(g, _) => XGridStats.IsCurrentAboveVah(g)
		),
		new XGridSignalDef(
			"above-val",
			"Above",
			"Above VAL",
			"Current price is above the Value Area Low's price",
			(g, _) => XGridStats.IsCurrentAboveVal(g)
		),
		new XGridSignalDef(
			"above-vwap",
			"Above",
			"Above VWAP",
			"Current price is above the VWAP's price",
			(g, _) => XGridStats.IsCurrentAboveVwap(g)
		),
		new XGridSignalDef(
			Keys.BelowPoc,
			"Below",
			"Below POC",
			"Current price is below the Point of Control's price",
			(g, _) => XGridStats.IsCurrentBelowPoc(g)
		),
		new XGridSignalDef(
			"below-vah",
			"Below",
			"Below VAH",
			"Current price is below the Value Area High's price",
			(g, _) => XGridStats.IsCurrentBelowVah(g)
		),
		new XGridSignalDef(
			"below-val",
			"Below",
			"Below VAL",
			"Current price is below the Value Area Low's price",
			(g, _) => XGridStats.IsCurrentBelowVal(g)
		),
		new XGridSignalDef(
			"below-vwap",
			"Below",
			"Below VWAP",
			"Current price is below the VWAP's price",
			(g, _) => XGridStats.IsCurrentBelowVwap(g)
		),
		new XGridSignalDef(
			"vwap-at-poc",
			"Vs POC",
			"VWAP At POC",
			"VWAP's row matches the Point of Control's row",
			(g, _) => XGridStats.IsVwapAtPoc(g)
		),
		new XGridSignalDef(
			"vwap-above-poc",
			"Vs POC",
			"VWAP Above POC",
			"VWAP's price is above the Point of Control's price",
			(g, _) => XGridStats.IsVwapAbovePoc(g)
		),
		new XGridSignalDef(
			"vwap-below-poc",
			"Vs POC",
			"VWAP Below POC",
			"VWAP's price is below the Point of Control's price",
			(g, _) => XGridStats.IsVwapBelowPoc(g)
		),
		new XGridSignalDef(
			"total-at-poc",
			"Vs POC",
			"Total At POC",
			"The grid's own total tick-count peak row matches the Point of Control's row",
			(g, _) => XGridStats.IsTotalAtPoc(g)
		),
		new XGridSignalDef(
			"total-above-poc",
			"Vs POC",
			"Total Above POC",
			"The grid's own total tick-count peak row is above the Point of Control's price",
			(g, _) => XGridStats.IsTotalAbovePoc(g)
		),
		new XGridSignalDef(
			"total-below-poc",
			"Vs POC",
			"Total Below POC",
			"The grid's own total tick-count peak row is below the Point of Control's price",
			(g, _) => XGridStats.IsTotalBelowPoc(g)
		),
		new XGridSignalDef(
			"up-at-poc",
			"Vs POC",
			"Up At POC",
			"The grid's own up-tick peak row matches the Point of Control's row",
			(g, _) => XGridStats.IsUpAtPoc(g)
		),
		new XGridSignalDef(
			"up-above-poc",
			"Vs POC",
			"Up Above POC",
			"The grid's own up-tick peak row is above the Point of Control's price",
			(g, _) => XGridStats.IsUpAbovePoc(g)
		),
		new XGridSignalDef(
			"up-below-poc",
			"Vs POC",
			"Up Below POC",
			"The grid's own up-tick peak row is below the Point of Control's price",
			(g, _) => XGridStats.IsUpBelowPoc(g)
		),
		new XGridSignalDef(
			"down-at-poc",
			"Vs POC",
			"Down At POC",
			"The grid's own down-tick peak row matches the Point of Control's row",
			(g, _) => XGridStats.IsDownAtPoc(g)
		),
		new XGridSignalDef(
			"down-above-poc",
			"Vs POC",
			"Down Above POC",
			"The grid's own down-tick peak row is above the Point of Control's price",
			(g, _) => XGridStats.IsDownAbovePoc(g)
		),
		new XGridSignalDef(
			"down-below-poc",
			"Vs POC",
			"Down Below POC",
			"The grid's own down-tick peak row is below the Point of Control's price",
			(g, _) => XGridStats.IsDownBelowPoc(g)
		),
	};

	private static readonly Dictionary<string, XGridSignalDef> ByKey = All.ToDictionary(d => d.Key);

	public static XGridSignalDef Get(string key) => ByKey[key];

	public static bool Contains(string key) => ByKey.ContainsKey(key);

	public static bool Value(XGridResult grid, string key, double percent) => Get(key).Predicate(grid, percent);
}
