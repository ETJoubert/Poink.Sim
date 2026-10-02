using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Poink.Sim.Models;

namespace Poink.Sim.Analysis;

/// <summary>
/// Computes a barrier map over ticks held in memory. Each entry is scanned once per side, noting the first moment every
/// stop and every target size is reached and the open result at every time limit; every stop/target/limit cell is
/// then decided from those, so the grid size barely affects the cost. The same entries are then run again on a
/// scrambled copy of the price as the benchmark for what chance gives.
/// </summary>
internal sealed class BarrierMapCalculator
{
	private const int HistogramBins = 20;
	private const double StopHistogramReach = 1.5;

	// Fixed so the same range always scrambles the same way and runs are repeatable.
	private const int ScrambleSeed = 20_240_607;

	private readonly long[] _ticks;
	private readonly PricePath _actual;

	public BarrierMapCalculator(long[] ticks, float[] bid, float[] ask)
	{
		_ticks = ticks;
		_actual = new PricePath(bid, ask);
	}

	public BarrierMapResult Compute(BarrierMapRequest request, CancellationToken token, Action<double> reportFraction)
	{
		var entries = EntryIndices(request);
		double meanSpreadPips =
			entries.Count == 0 ? 0 : entries.Average(i => (double)(_actual.Ask[i] - _actual.Bid[i])) / request.PipSize;
		var layout = new GridLayout(request);

		var actual = Run(_actual, entries, request, layout, token, fraction => reportFraction(fraction / 2));
		var benchmark = Run(Scrambled(), entries, request, layout, token, fraction => reportFraction(0.5 + fraction / 2));
		return new BarrierMapResult(request, _ticks.Length, entries.Count, meanSpreadPips, Grids(actual, benchmark, layout));
	}

	private SideAccumulators Run(
		PricePath path,
		List<int> entries,
		BarrierMapRequest request,
		GridLayout layout,
		CancellationToken token,
		Action<double> reportFraction
	)
	{
		var totals = new SideAccumulators(layout);
		var gate = new object();
		int done = 0;

		Parallel.For(
			0,
			entries.Count,
			new ParallelOptions { CancellationToken = token },
			() => (Accumulators: new SideAccumulators(layout), Scratch: new EntryScratch(layout)),
			(n, _, local) =>
			{
				int entry = entries[n];
				Scan(path, entry, isLong: true, request, layout, local.Scratch);
				Decide(entry, local.Scratch, layout, local.Accumulators.Long);
				Scan(path, entry, isLong: false, request, layout, local.Scratch);
				Decide(entry, local.Scratch, layout, local.Accumulators.Short);

				int finished = Interlocked.Increment(ref done);
				if (finished % 500 == 0)
					reportFraction((double)finished / entries.Count);
				return local;
			},
			local =>
			{
				lock (gate)
					totals.Add(local.Accumulators);
			}
		);
		return totals;
	}

	// Each tick's mid-price move keeps its size and timing but gets a random direction, and each tick keeps its own
	// spread -- volatility, quiet spells and costs survive, any tendency to snap back or carry on does not.
	private PricePath Scrambled()
	{
		int count = _ticks.Length;
		var bid = new float[count];
		var ask = new float[count];
		var random = new Random(ScrambleSeed);
		double previousActualMid = 0,
			mid = 0;
		for (int j = 0; j < count; j++)
		{
			double actualMid = (_actual.Bid[j] + (double)_actual.Ask[j]) / 2.0;
			mid = j == 0 ? actualMid : mid + (random.Next(2) == 0 ? 1 : -1) * (actualMid - previousActualMid);
			previousActualMid = actualMid;
			double halfSpread = (_actual.Ask[j] - (double)_actual.Bid[j]) / 2.0;
			bid[j] = (float)(mid - halfSpread);
			ask[j] = (float)(mid + halfSpread);
		}
		return new PricePath(bid, ask);
	}

	private List<int> EntryIndices(BarrierMapRequest request)
	{
		var entries = new List<int>();
		long from = request.From.Ticks,
			to = request.To.Ticks,
			spacing = TimeSpan.FromSeconds(Math.Max(1, request.EntrySpacingSeconds)).Ticks;
		long next = from;
		for (int i = 0; i < _ticks.Length && _ticks[i] <= to; i++)
		{
			if (_ticks[i] < next)
				continue;
			entries.Add(i);
			next = _ticks[i] + spacing;
		}
		return entries;
	}

	// The scan starts on the entry tick itself, where the trade is already a spread down -- a stop no wider than the
	// spread is hit at once, as it would be live. It stops once every level is reached or the longest limit passes.
	private void Scan(PricePath path, int entry, bool isLong, BarrierMapRequest request, GridLayout layout, EntryScratch scratch)
	{
		scratch.Reset();
		double pip = request.PipSize;
		double entryPrice = isLong ? path.Ask[entry] : path.Bid[entry];
		long start = _ticks[entry];
		long deadline = start + layout.LimitTicks[^1];
		double lastPips = path.PipsAt(entry, isLong, entryPrice, pip);
		int nextStop = 0,
			nextTarget = 0,
			nextLimit = 0;

		for (int j = entry; j < _ticks.Length; j++)
		{
			long now = _ticks[j];
			while (nextLimit < layout.Limits && start + layout.LimitTicks[nextLimit] < now)
			{
				scratch.LimitPips[nextLimit] = lastPips;
				scratch.LimitCovered[nextLimit] = true;
				nextLimit++;
			}
			if (now > deadline)
				break;

			double pips = path.PipsAt(j, isLong, entryPrice, pip);
			lastPips = pips;
			while (nextStop < layout.Stops && -pips >= layout.StopPips[nextStop])
			{
				scratch.StopTicks[nextStop] = now;
				scratch.StopFillPips[nextStop] = pips;
				nextStop++;
			}
			while (nextTarget < layout.Targets && pips >= layout.TargetPips[nextTarget])
			{
				scratch.TargetTicks[nextTarget] = now;
				nextTarget++;
			}
			if (nextStop == layout.Stops && nextTarget == layout.Targets)
				break;
		}
	}

	private void Decide(int entry, EntryScratch scratch, GridLayout layout, CellAccumulator[] cells)
	{
		long start = _ticks[entry];
		for (int k = 0; k < layout.Limits; k++)
		{
			long end = start + layout.LimitTicks[k];
			for (int s = 0; s < layout.Stops; s++)
			{
				long stopAt = scratch.StopTicks[s];
				bool stopped = stopAt <= end;
				for (int t = 0; t < layout.Targets; t++)
				{
					long targetAt = scratch.TargetTicks[t];
					bool targeted = targetAt <= end;
					var cell = cells[layout.CellIndex(k, s, t)];
					if (stopped && (!targeted || stopAt <= targetAt))
						cell.Add(Outcome.Loss, scratch.StopFillPips[s], MinutesBetween(start, stopAt));
					else if (targeted)
						cell.Add(Outcome.Win, layout.TargetPips[t], MinutesBetween(start, targetAt));
					else if (scratch.LimitCovered[k])
						cell.Add(Outcome.Timeout, scratch.LimitPips[k], layout.LimitMinutes[k]);
				}
			}
		}
	}

	private static double MinutesBetween(long from, long to) => (to - from) / (double)TimeSpan.TicksPerMinute;

	private static List<BarrierMapGrid> Grids(SideAccumulators actual, SideAccumulators benchmark, GridLayout layout)
	{
		var grids = new List<BarrierMapGrid>();
		foreach (var side in new[] { BarrierSide.Long, BarrierSide.Short, BarrierSide.Both })
		{
			var actualCells = actual.For(side);
			var benchmarkCells = benchmark.For(side);
			for (int k = 0; k < layout.Limits; k++)
				grids.Add(new BarrierMapGrid(side, layout.LimitMinutes[k], CellsFor(actualCells, benchmarkCells, layout, k)));
		}
		return grids;
	}

	private static List<BarrierCell> CellsFor(CellAccumulator[] actual, CellAccumulator[] benchmark, GridLayout layout, int limit)
	{
		var result = new List<BarrierCell>(layout.Stops * layout.Targets);
		for (int s = 0; s < layout.Stops; s++)
			for (int t = 0; t < layout.Targets; t++)
			{
				int index = layout.CellIndex(limit, s, t);
				var cell = actual[index];
				var chance = benchmark[index];
				result.Add(
					new BarrierCell(
						layout.StopPips[s],
						layout.TargetPips[t],
						cell.Trades,
						cell.Wins,
						cell.Losses,
						cell.Timeouts,
						cell.MeanPips,
						cell.CiPips,
						cell.MeanMinutes,
						chance.Trades > 0 ? (double)chance.Wins / chance.Trades : null,
						chance.Trades > 0 ? chance.MeanPips : null,
						cell.PipsHistogram,
						cell.PipsLow,
						cell.PipsHigh,
						cell.MinutesHistogram
					)
				);
			}
		return result;
	}

	private enum Outcome
	{
		Win,
		Loss,
		Timeout,
	}

	/// <summary>One bid/ask series the trades are run on -- the recorded prices, or their scrambled copy.</summary>
	private sealed class PricePath
	{
		public PricePath(float[] bid, float[] ask)
		{
			Bid = bid;
			Ask = ask;
		}

		public float[] Bid { get; }
		public float[] Ask { get; }

		public double PipsAt(int index, bool isLong, double entryPrice, double pip) =>
			isLong ? (Bid[index] - entryPrice) / pip : (entryPrice - Ask[index]) / pip;
	}

	/// <summary>The sorted stop, target and time-limit sizes and where each cell sits in the flat accumulator arrays.</summary>
	private sealed class GridLayout
	{
		public GridLayout(BarrierMapRequest request)
		{
			StopPips = request.StopPips.ToArray();
			TargetPips = request.TargetPips.ToArray();
			LimitMinutes = request.TimeLimitMinutes.ToArray();
			LimitTicks = LimitMinutes.Select(minutes => TimeSpan.FromMinutes(minutes).Ticks).ToArray();
		}

		public double[] StopPips { get; }
		public double[] TargetPips { get; }
		public int[] LimitMinutes { get; }
		public long[] LimitTicks { get; }
		public int Stops => StopPips.Length;
		public int Targets => TargetPips.Length;
		public int Limits => LimitMinutes.Length;
		public int CellCount => Limits * Stops * Targets;

		public int CellIndex(int limit, int stop, int target) => (limit * Stops + stop) * Targets + target;

		public CellAccumulator NewCell(int index)
		{
			int target = index % Targets;
			int stop = index / Targets % Stops;
			int limit = index / (Targets * Stops);
			return new CellAccumulator(-StopHistogramReach * StopPips[stop], TargetPips[target], LimitMinutes[limit]);
		}
	}

	private sealed class SideAccumulators
	{
		public SideAccumulators(GridLayout layout)
		{
			Long = Enumerable.Range(0, layout.CellCount).Select(layout.NewCell).ToArray();
			Short = Enumerable.Range(0, layout.CellCount).Select(layout.NewCell).ToArray();
		}

		public CellAccumulator[] Long { get; }
		public CellAccumulator[] Short { get; }

		public void Add(SideAccumulators other)
		{
			for (int c = 0; c < Long.Length; c++)
			{
				Long[c].Add(other.Long[c]);
				Short[c].Add(other.Short[c]);
			}
		}

		public CellAccumulator[] For(BarrierSide side)
		{
			if (side == BarrierSide.Long)
				return Long;
			if (side == BarrierSide.Short)
				return Short;

			var both = new CellAccumulator[Long.Length];
			for (int c = 0; c < both.Length; c++)
			{
				both[c] = Long[c].Copy();
				both[c].Add(Short[c]);
			}
			return both;
		}
	}

	/// <summary>One entry's first stop/target times (long.MaxValue when never reached) and its open result at each limit.</summary>
	private sealed class EntryScratch
	{
		public EntryScratch(GridLayout layout)
		{
			StopTicks = new long[layout.Stops];
			StopFillPips = new double[layout.Stops];
			TargetTicks = new long[layout.Targets];
			LimitPips = new double[layout.Limits];
			LimitCovered = new bool[layout.Limits];
		}

		public long[] StopTicks { get; }
		public double[] StopFillPips { get; }
		public long[] TargetTicks { get; }
		public double[] LimitPips { get; }
		public bool[] LimitCovered { get; }

		public void Reset()
		{
			Array.Fill(StopTicks, long.MaxValue);
			Array.Fill(TargetTicks, long.MaxValue);
			Array.Clear(LimitCovered);
		}
	}

	private sealed class CellAccumulator
	{
		private readonly long[] _pipsHistogram = new long[HistogramBins];
		private readonly long[] _minutesHistogram = new long[HistogramBins];
		private readonly double _limitMinutes;
		private double _sumPips;
		private double _sumSquares;
		private double _sumMinutes;

		public CellAccumulator(double pipsLow, double pipsHigh, double limitMinutes)
		{
			PipsLow = pipsLow;
			PipsHigh = pipsHigh;
			_limitMinutes = limitMinutes;
		}

		public double PipsLow { get; }
		public double PipsHigh { get; }
		public int Trades { get; private set; }
		public int Wins { get; private set; }
		public int Losses { get; private set; }
		public int Timeouts { get; private set; }
		public IReadOnlyList<long> PipsHistogram => _pipsHistogram;
		public IReadOnlyList<long> MinutesHistogram => _minutesHistogram;
		public double MeanPips => Trades > 0 ? _sumPips / Trades : 0;
		public double MeanMinutes => Trades > 0 ? _sumMinutes / Trades : 0;

		public double? CiPips =>
			Trades > 1 ? 1.96 * Math.Sqrt(Math.Max(0, (_sumSquares - _sumPips * _sumPips / Trades) / (Trades - 1))) / Math.Sqrt(Trades) : null;

		public void Add(Outcome outcome, double pips, double minutes)
		{
			Trades++;
			switch (outcome)
			{
				case Outcome.Win:
					Wins++;
					break;
				case Outcome.Loss:
					Losses++;
					break;
				default:
					Timeouts++;
					break;
			}
			_sumPips += pips;
			_sumSquares += pips * pips;
			_sumMinutes += minutes;
			_pipsHistogram[Bin(pips, PipsLow, PipsHigh)]++;
			_minutesHistogram[Bin(minutes, 0, _limitMinutes)]++;
		}

		public void Add(CellAccumulator other)
		{
			Trades += other.Trades;
			Wins += other.Wins;
			Losses += other.Losses;
			Timeouts += other.Timeouts;
			_sumPips += other._sumPips;
			_sumSquares += other._sumSquares;
			_sumMinutes += other._sumMinutes;
			for (int b = 0; b < HistogramBins; b++)
			{
				_pipsHistogram[b] += other._pipsHistogram[b];
				_minutesHistogram[b] += other._minutesHistogram[b];
			}
		}

		public CellAccumulator Copy()
		{
			var copy = new CellAccumulator(PipsLow, PipsHigh, _limitMinutes);
			copy.Add(this);
			return copy;
		}

		// Values outside the range land in the end bins, so a gap through a stop still counts in the leftmost bin.
		private static int Bin(double value, double low, double high) =>
			high <= low ? 0 : Math.Clamp((int)((value - low) / (high - low) * HistogramBins), 0, HistogramBins - 1);
	}
}
