using System;
using System.Collections.Generic;

namespace Poink.Sim.Models;

/// <summary>
/// A barrier map run: a trade opened every <see cref="EntrySpacingSeconds"/> through the range, on each side, tested
/// against every stop-loss and take-profit size (in pips) and every time limit.
/// </summary>
public sealed record BarrierMapRequest(
	string Table,
	DateTime From,
	DateTime To,
	double PipSize,
	IReadOnlyList<double> StopPips,
	IReadOnlyList<double> TargetPips,
	IReadOnlyList<int> TimeLimitMinutes,
	int EntrySpacingSeconds
);

public enum BarrierSide
{
	Long,
	Short,
	Both,
}

/// <summary>
/// What happened to the trades with one stop/target pair at one time limit. Results are in pips after the spread; a
/// stop fills at the price that crossed it, a target at the target, and a trade still open at the limit at the market.
/// The benchmark figures are the same trades run on a scrambled copy of the price -- same tick times, spreads and move
/// sizes, random direction -- which is what chance gives under the same time limit and costs.
/// </summary>
public sealed record BarrierCell(
	double StopPips,
	double TargetPips,
	int Trades,
	int Wins,
	int Losses,
	int Timeouts,
	double MeanPips,
	double? CiPips,
	double MeanMinutes,
	double? BenchmarkWinRate,
	double? BenchmarkMeanPips,
	IReadOnlyList<long> PipsHistogram,
	double PipsHistogramLow,
	double PipsHistogramHigh,
	IReadOnlyList<long> MinutesHistogram
)
{
	public double? WinRate => Trades > 0 ? (double)Wins / Trades : null;

	/// <summary>How far the win rate beats the same trades on the scrambled price.</summary>
	public double? Edge => WinRate - BenchmarkWinRate;

	/// <summary>How many pips per trade better than the same trades on the scrambled price.</summary>
	public double? EdgePips => Trades > 0 ? MeanPips - BenchmarkMeanPips : null;
}

/// <summary>Every stop/target cell for one side and time limit, stop-major: cell [s * targets + t].</summary>
public sealed record BarrierMapGrid(BarrierSide Side, int TimeLimitMinutes, IReadOnlyList<BarrierCell> Cells);

public sealed record BarrierMapResult(
	BarrierMapRequest Request,
	long TickCount,
	long EntryCount,
	double MeanSpreadPips,
	IReadOnlyList<BarrierMapGrid> Grids
)
{
	public BarrierMapGrid? Grid(BarrierSide side, int timeLimitMinutes)
	{
		foreach (var grid in Grids)
			if (grid.Side == side && grid.TimeLimitMinutes == timeLimitMinutes)
				return grid;
		return null;
	}
}

/// <summary>Which figure the barrier map's heatmap colours its cells by.</summary>
public enum BarrierMetric
{
	MeanPips,
	WinRate,
	Edge,
	MeanMinutes,
	Trades,
}

/// <summary>What the barrier map is showing: one side and time limit, coloured by one metric, with one cell picked out.</summary>
public sealed record BarrierMapSelection(BarrierSide Side, int TimeLimitMinutes, BarrierMetric Metric, int? CellIndex);
