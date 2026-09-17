using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Analysis;

internal sealed class PriceReturnAnalysisService : IPriceReturnAnalysisService
{
	private const int BucketCount = 20;
	private const int ProgressReportInterval = 5_000;

	private readonly object _lock = new();
	private readonly ITickRepository _tickRepository;
	private readonly ILogger<PriceReturnAnalysisService> _logger;
	private volatile bool _cancelRequested;

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

	// Streams ticks once; for every tick in [From, To] the forward window [t, t+X] is watched via a
	// running min/max -- once that range spans back over the start price, the point has "returned".
	private PriceReturnDistributionResult Compute(PriceReturnRequest request)
	{
		var windowSpan = TimeSpan.FromMinutes(request.WindowMinutes);
		var readTo = request.To.Add(windowSpan);

		var totalTicks = _tickRepository.CountTicks(request.Table, request.From, readTo);
		long processed = 0;

		var open = new List<OpenStart>();
		var results = new List<(bool Returned, double MinutesToReturn)>();

		foreach (var tick in _tickRepository.ReadTicks(request.Table, request.From, readTo))
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

			if (tick.Timestamp >= request.From && tick.Timestamp <= request.To)
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

			processed++;
			if (processed % ProgressReportInterval == 0)
			{
				lock (_lock)
					ProgressPercent = totalTicks > 0 ? Math.Min(100, processed * 100.0 / totalTicks) : 0;
				OnChanged?.Invoke();
			}
		}

		lock (_lock)
			ProgressPercent = 100;
		OnChanged?.Invoke();

		return BuildResult(results, request.WindowMinutes);
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
