using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Analysis;

internal sealed class PriceReturnAnalysisService : IPriceReturnAnalysisService
{
	private const int BucketCount = 20;
	private const int ProgressReportInterval = 5_000;

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
		if (string.IsNullOrWhiteSpace(request.Table) || request.To <= request.From || request.WindowMinutes <= 0)
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
			"Price-return analysis started: table={Table} from={From} to={To} window={WindowMinutes}m",
			request.Table,
			request.From,
			request.To,
			request.WindowMinutes
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

	private PriceReturnDistributionResult Compute(PriceReturnRequest request)
	{
		var windowSpan = TimeSpan.FromMinutes(request.WindowMinutes);
		var totalTicks = _tickRepository.CountTicks(request.Table, request.From, request.To.Add(windowSpan));
		_processedCount = 0;

		var chunks = SplitIntoChunks(request.From, request.To, WorkerCount);
		var chunkResults = new List<(bool Returned, double MinutesToReturn)>[chunks.Count];

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

		var results = new List<(bool Returned, double MinutesToReturn)>(chunkResults.Sum(c => c.Count));
		foreach (var chunk in chunkResults)
			results.AddRange(chunk);

		return BuildResult(results, request.WindowMinutes);
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
	private List<(bool Returned, double MinutesToReturn)> ComputeChunk(
		string table,
		DateTime chunkFrom,
		DateTime chunkTo,
		bool isLastChunk,
		TimeSpan windowSpan,
		long totalTicks
	)
	{
		var open = new List<OpenStart>();
		var results = new List<(bool Returned, double MinutesToReturn)>();

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

	private static PriceReturnDistributionResult BuildResult(
		List<(bool Returned, double MinutesToReturn)> results,
		double windowMinutes
	)
	{
		if (results.Count == 0)
			return new PriceReturnDistributionResult(0, 0, 0, 0, 0, Array.Empty<HistogramBin>());

		var returnedTimes = results.Where(r => r.Returned).Select(r => r.MinutesToReturn).OrderBy(t => t).ToList();
		var probability = (double)returnedTimes.Count / results.Count;
		var mean = returnedTimes.Count > 0 ? returnedTimes.Average() : 0;
		var median = returnedTimes.Count > 0 ? returnedTimes[returnedTimes.Count / 2] : 0;

		var counts = new long[BucketCount];
		foreach (var minutes in returnedTimes)
			counts[(int)Math.Clamp(minutes / windowMinutes * BucketCount, 0, BucketCount - 1)]++;

		var bins = new List<HistogramBin>(BucketCount);
		for (var i = 0; i < BucketCount; i++)
			bins.Add(
				new HistogramBin((double)i / BucketCount * windowMinutes, (double)(i + 1) / BucketCount * windowMinutes, counts[i])
			);

		return new PriceReturnDistributionResult(results.Count, returnedTimes.Count, probability, mean, median, bins);
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
