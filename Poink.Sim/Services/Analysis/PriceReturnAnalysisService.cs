using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Simulation;

namespace Poink.Sim.Analysis;

internal sealed class PriceReturnAnalysisService : IPriceReturnAnalysisService
{
	private const int DefaultBucketCount = 20;
	private const int ProgressReportInterval = 5_000;

	// Only used by SimulationBarAggregator's discarded histogram side-channel; aggregation mode never reads it.
	private const double PriceBucketWidth = 0.01;

	// A start point's fate depends only on ticks up to windowSpan past it, so the range splits into
	// WorkerCount independent chunks, computed in parallel, then merges into one result.
	private static readonly int WorkerCount = Math.Clamp(Environment.ProcessorCount, 1, 32);

	private readonly object _lock = new();
	private readonly ITickRepository _tickRepository;
	private readonly ILogger<PriceReturnAnalysisService> _logger;
	private volatile bool _cancelRequested;
	private long _processedCount;

	public PriceReturnAnalysisService(ITickRepository tickRepository, ILogger<PriceReturnAnalysisService> logger)
	{
		_tickRepository = tickRepository;
		_logger = logger;
	}

	public bool IsRunning { get; private set; }
	public double ProgressPercent { get; private set; }
	public string? Error { get; private set; }
	public bool WasCancelled { get; private set; }
	public TimeSpan? Elapsed { get; private set; }
	public PriceReturnDistributionResult? Result { get; private set; }

	public event Action? OnChanged;

	public bool TryStart(PriceReturnRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.Table) || request.To <= request.From)
			return false;
		if (request.AggregationBarWidthSeconds is { } barWidthSeconds)
		{
			if (barWidthSeconds <= 0 || request.AggregationWindowBars is not { } windowBars || windowBars <= 0)
				return false;
		}
		else if (request.WindowMinutes <= 0)
			return false;

		lock (_lock)
		{
			if (IsRunning)
				return false;
			IsRunning = true;
			ProgressPercent = 0;
			Error = null;
			WasCancelled = false;
			Result = null;
			Elapsed = null;
			_cancelRequested = false;
		}
		OnChanged?.Invoke();

		_logger.LogInformation(
			"Price-return analysis started: table={Table} from={From} to={To} window={WindowMinutes}m aggregationBarSeconds={AggregationBarWidthSeconds} aggregationWindowBars={AggregationWindowBars} aggregationKind={AggregationKind}",
			request.Table,
			request.From,
			request.To,
			request.WindowMinutes,
			request.AggregationBarWidthSeconds,
			request.AggregationWindowBars,
			request.AggregationKind
		);

		_ = Task.Run(() => Run(request));
		return true;
	}

	public void Cancel() => _cancelRequested = true;

	private void Run(PriceReturnRequest request)
	{
		var stopwatch = Stopwatch.StartNew();
		try
		{
			var result = Compute(request);
			lock (_lock)
			{
				Result = result;
				Elapsed = stopwatch.Elapsed;
			}
			_logger.LogInformation(
				"Price-return analysis finished: samples={SampleCount} returned={ReturnedCount} elapsed={Elapsed}",
				result.SampleCount,
				result.ReturnedCount,
				stopwatch.Elapsed
			);
		}
		catch (OperationCanceledException)
		{
			lock (_lock)
				WasCancelled = true;
			_logger.LogWarning("Price-return analysis cancelled after {Elapsed}", stopwatch.Elapsed);
		}
		catch (Exception ex)
		{
			lock (_lock)
				Error = ex.Message;
			_logger.LogError(ex, "Price-return analysis failed");
		}
		finally
		{
			lock (_lock)
				IsRunning = false;
			OnChanged?.Invoke();
		}
	}

	private PriceReturnDistributionResult Compute(PriceReturnRequest request) =>
		request.AggregationBarWidthSeconds is { } barWidthSeconds
			? ComputeFromBars(request, TimeSpan.FromSeconds(barWidthSeconds), request.AggregationWindowBars!.Value)
			: ComputeFromTicks(request);

	private PriceReturnDistributionResult ComputeFromTicks(PriceReturnRequest request)
	{
		var windowSpan = TimeSpan.FromMinutes(request.WindowMinutes);
		var totalTicks = _tickRepository.CountTicks(request.Table, request.From, request.To.Add(windowSpan));
		_processedCount = 0;

		var chunks = SplitIntoChunks(request.From, request.To, WorkerCount);
		var chunkResults = new List<(bool Returned, double Value)>[chunks.Count];

		try
		{
			Parallel.For(
				0,
				chunks.Count,
				i =>
				{
					var (chunkFrom, chunkTo) = chunks[i];
					chunkResults[i] = ComputeChunk(request.Table, chunkFrom, chunkTo, i == chunks.Count - 1, windowSpan, totalTicks);
				}
			);
		}
		catch (AggregateException ex) when (ex.Flatten().InnerExceptions.Any(e => e is OperationCanceledException))
		{
			throw new OperationCanceledException();
		}

		lock (_lock)
			ProgressPercent = 100;
		OnChanged?.Invoke();

		var results = new List<(bool Returned, double Value)>(chunkResults.Sum(c => c.Count));
		foreach (var chunk in chunkResults)
			results.AddRange(chunk);

		return BuildResult(results, request.WindowMinutes, DefaultBucketCount, "min");
	}

	// Half-open per chunk ([from, to)) except the last, which keeps the overall range's own closed
	// end -- otherwise a tick exactly on a chunk boundary would become a start point in both chunks.
	private static List<(DateTime From, DateTime To)> SplitIntoChunks(DateTime from, DateTime to, int count)
	{
		var chunkTicks = (to - from).Ticks / count;
		var chunks = new List<(DateTime From, DateTime To)>(count);
		var start = from;
		for (var i = 0; i < count; i++)
		{
			var end = i == count - 1 ? to : start.AddTicks(chunkTicks);
			chunks.Add((start, end));
			start = end;
		}
		return chunks;
	}

	// Streams a chunk's ticks once; for every tick in it the forward window [t, t+X] is watched via
	// a running min/max -- once that range spans back over the start price, the point has "returned".
	private List<(bool Returned, double Value)> ComputeChunk(
		string table,
		DateTime chunkFrom,
		DateTime chunkTo,
		bool isLastChunk,
		TimeSpan windowSpan,
		long totalTicks
	)
	{
		var open = new List<OpenStart>();
		var results = new List<(bool Returned, double Value)>();

		foreach (var tick in _tickRepository.ReadTicks(table, chunkFrom, chunkTo.Add(windowSpan)))
		{
			if (_cancelRequested)
				throw new OperationCanceledException();

			var mid = (tick.Bid + tick.Ask) / 2.0;

			for (var i = open.Count - 1; i >= 0; i--)
			{
				var o = open[i];
				if (o.ReturnTime is null)
				{
					if (mid < o.RunningMin)
						o.RunningMin = mid;
					if (mid > o.RunningMax)
						o.RunningMax = mid;
					if (o.RunningMin <= o.StartPrice && o.RunningMax >= o.StartPrice)
						o.ReturnTime = tick.Timestamp;
				}
				if (tick.Timestamp >= o.WindowEnd)
				{
					results.Add(
						o.ReturnTime is { } returnTime
							? (true, (returnTime - o.StartTime).TotalMinutes)
							: (false, 0)
					);
					open[i] = open[^1];
					open.RemoveAt(open.Count - 1);
				}
			}

			if (tick.Timestamp >= chunkFrom && (isLastChunk ? tick.Timestamp <= chunkTo : tick.Timestamp < chunkTo))
			{
				open.Add(
					new OpenStart
					{
						StartPrice = mid,
						StartTime = tick.Timestamp,
						WindowEnd = tick.Timestamp + windowSpan,
						RunningMin = mid,
						RunningMax = mid,
					}
				);
			}

			var processed = Interlocked.Increment(ref _processedCount);
			if (processed % ProgressReportInterval == 0)
			{
				lock (_lock)
					ProgressPercent = totalTicks > 0 ? Math.Min(100, processed * 100.0 / totalTicks) : 0;
				OnChanged?.Invoke();
			}
		}

		return results;
	}

	// Aggregation has no clock, just bar positions, so windowBars counts subsequent bars, not minutes.
	private PriceReturnDistributionResult ComputeFromBars(PriceReturnRequest request, TimeSpan barSize, int windowBars) =>
		request.AggregationKind == BarAggregationKind.Quartile
			? ComputeFromQuartileBars(request, barSize, windowBars)
			: ComputeFromOhlcBars(request, barSize, windowBars);

	// Runs once per OHLC value: that value on the starting bar, checked against later bars' own true traded range (Low/High).
	private PriceReturnDistributionResult ComputeFromOhlcBars(PriceReturnRequest request, TimeSpan barSize, int windowBars)
	{
		var windowSpan = TimeSpan.FromTicks(barSize.Ticks * windowBars);
		var bars = AggregateBars(request.Table, request.From, request.To.Add(windowSpan), barSize);

		var closeResults = ComputeSeriesFromBars(bars, barSize, windowBars, request.From, request.To, b => b.OpenTime, b => b.Low, b => b.High, b => b.Close);
		var openSeries = ToSeriesResult(
			"Open",
			ComputeSeriesFromBars(bars, barSize, windowBars, request.From, request.To, b => b.OpenTime, b => b.Low, b => b.High, b => b.Open),
			windowBars,
			windowBars
		);
		var highSeries = ToSeriesResult(
			"High",
			ComputeSeriesFromBars(bars, barSize, windowBars, request.From, request.To, b => b.OpenTime, b => b.Low, b => b.High, b => b.High),
			windowBars,
			windowBars
		);
		var lowSeries = ToSeriesResult(
			"Low",
			ComputeSeriesFromBars(bars, barSize, windowBars, request.From, request.To, b => b.OpenTime, b => b.Low, b => b.High, b => b.Low),
			windowBars,
			windowBars
		);
		var closeSeries = ToSeriesResult("Close", closeResults, windowBars, windowBars);

		lock (_lock)
			ProgressPercent = 100;
		OnChanged?.Invoke();

		var closeResult = BuildResult(closeResults, windowBars, windowBars, "bars");
		return closeResult with { SeriesBreakdown = new[] { openSeries, highSeries, lowSeries, closeSeries } };
	}

	// Same principle as the OHLC path, but the starting/reference value and the later bars' traded
	// range both come from each bar's Min/Q1/Median/Q3/Max five-number summary instead of Open/High/Low/Close.
	private PriceReturnDistributionResult ComputeFromQuartileBars(PriceReturnRequest request, TimeSpan barSize, int windowBars)
	{
		var windowSpan = TimeSpan.FromTicks(barSize.Ticks * windowBars);
		var bars = AggregateQuartileBars(request.Table, request.From, request.To.Add(windowSpan), barSize);

		var medianResults = ComputeSeriesFromBars(bars, barSize, windowBars, request.From, request.To, b => b.OpenTime, b => b.Min, b => b.Max, b => b.Median);
		var minSeries = ToSeriesResult(
			"Min",
			ComputeSeriesFromBars(bars, barSize, windowBars, request.From, request.To, b => b.OpenTime, b => b.Min, b => b.Max, b => b.Min),
			windowBars,
			windowBars
		);
		var q1Series = ToSeriesResult(
			"Q1",
			ComputeSeriesFromBars(bars, barSize, windowBars, request.From, request.To, b => b.OpenTime, b => b.Min, b => b.Max, b => b.Q1),
			windowBars,
			windowBars
		);
		var medianSeries = ToSeriesResult("Median", medianResults, windowBars, windowBars);
		var q3Series = ToSeriesResult(
			"Q3",
			ComputeSeriesFromBars(bars, barSize, windowBars, request.From, request.To, b => b.OpenTime, b => b.Min, b => b.Max, b => b.Q3),
			windowBars,
			windowBars
		);
		var maxSeries = ToSeriesResult(
			"Max",
			ComputeSeriesFromBars(bars, barSize, windowBars, request.From, request.To, b => b.OpenTime, b => b.Min, b => b.Max, b => b.Max),
			windowBars,
			windowBars
		);

		lock (_lock)
			ProgressPercent = 100;
		OnChanged?.Invoke();

		var medianResult = BuildResult(medianResults, windowBars, windowBars, "bars");
		return medianResult with { SeriesBreakdown = new[] { minSeries, q1Series, medianSeries, q3Series, maxSeries } };
	}

	// Scans forward from each starting bar, at most windowBars bars, for the first later bar whose
	// selected [low, high] range contains the reference value; a window running past the data we have is dropped.
	private List<(bool Returned, double Value)> ComputeSeriesFromBars<TBar>(
		List<TBar> bars,
		TimeSpan barSize,
		int windowBars,
		DateTime from,
		DateTime to,
		Func<TBar, DateTime> selectOpenTime,
		Func<TBar, double> selectLow,
		Func<TBar, double> selectHigh,
		Func<TBar, double> selectStartValue
	)
	{
		var results = new List<(bool Returned, double Value)>();

		for (var i = 0; i < bars.Count; i++)
		{
			if (_cancelRequested)
				throw new OperationCanceledException();

			var barEndTime = selectOpenTime(bars[i]) + barSize;
			if (barEndTime < from || barEndTime > to)
				continue;

			var lastIndex = i + windowBars;
			if (lastIndex >= bars.Count)
				continue;

			var startValue = selectStartValue(bars[i]);
			var runningMin = double.PositiveInfinity;
			var runningMax = double.NegativeInfinity;
			var returnedAtOffset = -1;
			for (var j = i + 1; j <= lastIndex; j++)
			{
				var low = selectLow(bars[j]);
				var high = selectHigh(bars[j]);
				if (low < runningMin)
					runningMin = low;
				if (high > runningMax)
					runningMax = high;
				if (runningMin <= startValue && runningMax >= startValue)
				{
					returnedAtOffset = j - i;
					break;
				}
			}

			// 0-indexed (bar 1 after the start = offset 0) so it buckets 1:1 with ComputeStats' bucketCount=windowBars.
			results.Add(returnedAtOffset > 0 ? (true, returnedAtOffset - 1) : (false, 0));
		}

		return results;
	}

	private List<QuartileBar> AggregateQuartileBars(string table, DateTime from, DateTime to, TimeSpan barSize)
	{
		var totalTicks = _tickRepository.CountTicks(table, from, to);
		long processed = 0;

		var aggregator = new QuartileBarAggregator(barSize);
		var bars = new List<QuartileBar>();
		foreach (var tick in _tickRepository.ReadTicks(table, from, to))
		{
			if (_cancelRequested)
				throw new OperationCanceledException();

			if (aggregator.Feed(tick) is { } completed)
				bars.Add(completed);

			processed++;
			if (processed % ProgressReportInterval == 0)
			{
				lock (_lock)
					ProgressPercent = totalTicks > 0 ? Math.Min(90, processed * 90.0 / totalTicks) : 0;
				OnChanged?.Invoke();
			}
		}
		if (aggregator.CurrentPartialBar is { } partial)
			bars.Add(partial);

		return bars;
	}

	private List<HlocAvdtBar> AggregateBars(string table, DateTime from, DateTime to, TimeSpan barSize)
	{
		var totalTicks = _tickRepository.CountTicks(table, from, to);
		long processed = 0;

		var aggregator = new SimulationBarAggregator(barSize, PriceBucketWidth);
		var bars = new List<HlocAvdtBar>();
		foreach (var tick in _tickRepository.ReadTicks(table, from, to))
		{
			if (_cancelRequested)
				throw new OperationCanceledException();

			if (aggregator.Feed(tick) is { } completed)
				bars.Add(completed.Bar);

			processed++;
			if (processed % ProgressReportInterval == 0)
			{
				lock (_lock)
					ProgressPercent = totalTicks > 0 ? Math.Min(90, processed * 90.0 / totalTicks) : 0;
				OnChanged?.Invoke();
			}
		}
		if (aggregator.CurrentPartialBar is { } partial)
			bars.Add(partial);

		return bars;
	}

	private static PriceReturnDistributionResult BuildResult(
		List<(bool Returned, double Value)> results,
		double windowSpanUnits,
		int bucketCount,
		string unit
	)
	{
		var stats = ComputeStats(results, windowSpanUnits, bucketCount);
		return new PriceReturnDistributionResult(
			stats.SampleCount,
			stats.ReturnedCount,
			stats.Probability,
			stats.Mean,
			stats.Median,
			stats.Bins,
			unit
		);
	}

	private static PriceReturnSeriesResult ToSeriesResult(
		string series,
		List<(bool Returned, double Value)> results,
		double windowSpanUnits,
		int bucketCount
	)
	{
		var stats = ComputeStats(results, windowSpanUnits, bucketCount);
		return new PriceReturnSeriesResult(series, stats.ReturnedCount, stats.Probability, stats.Mean, stats.Median, stats.Bins);
	}

	private static (long SampleCount, long ReturnedCount, double Probability, double Mean, double Median, List<HistogramBin> Bins) ComputeStats(
		List<(bool Returned, double Value)> results,
		double windowSpanUnits,
		int bucketCount
	)
	{
		if (results.Count == 0)
			return (0, 0, 0, 0, 0, new List<HistogramBin>());

		var returnedValues = results.Where(r => r.Returned).Select(r => r.Value).OrderBy(v => v).ToList();
		var probability = (double)returnedValues.Count / results.Count;
		var mean = returnedValues.Count > 0 ? returnedValues.Average() : 0;
		var median = returnedValues.Count > 0 ? returnedValues[returnedValues.Count / 2] : 0;

		var counts = new long[bucketCount];
		foreach (var value in returnedValues)
			counts[(int)Math.Clamp(value / windowSpanUnits * bucketCount, 0, bucketCount - 1)]++;

		var bins = new List<HistogramBin>(bucketCount);
		for (var i = 0; i < bucketCount; i++)
			bins.Add(
				new HistogramBin(
					(double)i / bucketCount * windowSpanUnits,
					(double)(i + 1) / bucketCount * windowSpanUnits,
					counts[i]
				)
			);

		return (results.Count, returnedValues.Count, probability, mean, median, bins);
	}

	private sealed class OpenStart
	{
		public double StartPrice;
		public DateTime StartTime;
		public DateTime WindowEnd;
		public double RunningMin;
		public double RunningMax;
		public DateTime? ReturnTime;
	}
}
