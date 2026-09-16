using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Analysis;

internal sealed class TimeAboveBelowAnalysisService : ITimeAboveBelowAnalysisService
{
	private const int BucketCount = 20;
	private const int ProgressReportInterval = 5_000;

	private readonly object _lock = new();
	private readonly ITickRepository _tickRepository;
	private readonly ILogger<TimeAboveBelowAnalysisService> _logger;
	private volatile bool _cancelRequested;

	public TimeAboveBelowAnalysisService(ITickRepository tickRepository, ILogger<TimeAboveBelowAnalysisService> logger)
	{
		_tickRepository = tickRepository;
		_logger = logger;
	}

	public bool IsRunning { get; private set; }
	public double ProgressPercent { get; private set; }
	public string? Error { get; private set; }
	public bool WasCancelled { get; private set; }
	public TimeSpan? Elapsed { get; private set; }
	public TimeAboveBelowDistributionResult? Result { get; private set; }

	public event Action? OnChanged;

	public bool TryStart(TimeAboveBelowRequest request)
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
			"Time-above/below analysis started: table={Table} from={From} to={To} window={WindowMinutes}m",
			request.Table,
			request.From,
			request.To,
			request.WindowMinutes
		);

		_ = Task.Run(() => Run(request));
		return true;
	}

	public void Cancel() => _cancelRequested = true;

	private void Run(TimeAboveBelowRequest request)
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
				"Time-above/below analysis finished: samples={SampleCount} elapsed={Elapsed}",
				result.SampleCount,
				stopwatch.Elapsed
			);
		}
		catch (OperationCanceledException)
		{
			lock (_lock)
				WasCancelled = true;
			_logger.LogWarning("Time-above/below analysis cancelled after {Elapsed}", stopwatch.Elapsed);
		}
		catch (Exception ex)
		{
			lock (_lock)
				Error = ex.Message;
			_logger.LogError(ex, "Time-above/below analysis failed");
		}
		finally
		{
			lock (_lock)
				IsRunning = false;
			OnChanged?.Invoke();
		}
	}

	// Streams ticks once; for every tick in [From, To] the window [t, t+X] is tracked by an
	// open accumulator until data past its end arrives, at which point it closes into a fraction.
	private TimeAboveBelowDistributionResult Compute(TimeAboveBelowRequest request)
	{
		var windowSpan = TimeSpan.FromMinutes(request.WindowMinutes);
		var readTo = request.To.Add(windowSpan);

		var totalTicks = _tickRepository.CountTicks(request.Table, request.From, readTo);
		long processed = 0;

		var open = new List<OpenStart>();
		var fractions = new List<double>();

		Tick? prev = null;
		foreach (var tick in _tickRepository.ReadTicks(request.Table, request.From, readTo))
		{
			if (_cancelRequested)
				throw new OperationCanceledException();

			if (prev is { } previous)
			{
				var prevMid = (previous.Bid + previous.Ask) / 2.0;
				for (var i = open.Count - 1; i >= 0; i--)
				{
					var o = open[i];
					var segmentEnd = tick.Timestamp < o.WindowEnd ? tick.Timestamp : o.WindowEnd;
					if (segmentEnd > o.LastTime)
					{
						if (prevMid > o.StartPrice)
							o.SecondsAbove += (segmentEnd - o.LastTime).TotalSeconds;
						o.LastTime = segmentEnd;
					}
					if (o.LastTime >= o.WindowEnd)
					{
						fractions.Add(o.SecondsAbove / windowSpan.TotalSeconds);
						open[i] = open[^1];
						open.RemoveAt(open.Count - 1);
					}
				}
			}

			if (tick.Timestamp >= request.From && tick.Timestamp <= request.To)
			{
				open.Add(
					new OpenStart
					{
						StartPrice = (tick.Bid + tick.Ask) / 2.0,
						WindowEnd = tick.Timestamp + windowSpan,
						LastTime = tick.Timestamp,
						SecondsAbove = 0,
					}
				);
			}

			prev = tick;
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

		return BuildResult(fractions);
	}

	private static TimeAboveBelowDistributionResult BuildResult(List<double> fractions)
	{
		if (fractions.Count == 0)
			return new TimeAboveBelowDistributionResult(0, 0, 0, 0, 0, 0, Array.Empty<HistogramBin>());

		fractions.Sort();
		var mean = fractions.Average();
		var median = fractions[fractions.Count / 2];
		var variance = fractions.Sum(f => (f - mean) * (f - mean)) / fractions.Count;
		var stdDev = Math.Sqrt(variance);

		var counts = new long[BucketCount];
		foreach (var f in fractions)
			counts[(int)Math.Clamp(f * BucketCount, 0, BucketCount - 1)]++;

		var bins = new List<HistogramBin>(BucketCount);
		for (var i = 0; i < BucketCount; i++)
			bins.Add(new HistogramBin((double)i / BucketCount, (double)(i + 1) / BucketCount, counts[i]));

		return new TimeAboveBelowDistributionResult(
			fractions.Count,
			mean,
			median,
			stdDev,
			fractions[0],
			fractions[^1],
			bins
		);
	}

	private sealed class OpenStart
	{
		public double StartPrice;
		public DateTime WindowEnd;
		public DateTime LastTime;
		public double SecondsAbove;
	}
}
