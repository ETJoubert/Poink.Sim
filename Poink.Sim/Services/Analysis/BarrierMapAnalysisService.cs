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

internal sealed class BarrierMapAnalysisService : IBarrierMapAnalysisService
{
	private const double LoadShare = 0.3;
	private const int LoadProgressTicks = 250_000;

	private readonly object _lock = new();
	private readonly ITickRepository _tickRepository;
	private readonly ILogger<BarrierMapAnalysisService> _logger;
	private CancellationTokenSource? _cancellation;

	public BarrierMapAnalysisService(ITickRepository tickRepository, ILogger<BarrierMapAnalysisService> logger)
	{
		_tickRepository = tickRepository;
		_logger = logger;
	}

	public bool IsRunning { get; private set; }
	public double ProgressPercent { get; private set; }
	public string? Error { get; private set; }
	public bool WasCancelled { get; private set; }
	public TimeSpan? Elapsed { get; private set; }
	public BarrierMapResult? Result { get; private set; }

	public event Action? OnChanged;

	public bool TryStart(BarrierMapRequest request)
	{
		if (
			string.IsNullOrWhiteSpace(request.Table)
			|| request.To <= request.From
			|| request.PipSize <= 0
			|| request.StopPips.Count == 0
			|| request.TargetPips.Count == 0
			|| request.TimeLimitMinutes.Count == 0
		)
			return false;

		var normalized = request with
		{
			StopPips = request.StopPips.Where(p => p > 0).Distinct().OrderBy(p => p).ToList(),
			TargetPips = request.TargetPips.Where(p => p > 0).Distinct().OrderBy(p => p).ToList(),
			TimeLimitMinutes = request.TimeLimitMinutes.Where(m => m > 0).Distinct().OrderBy(m => m).ToList(),
		};
		CancellationToken token;
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
			_cancellation = new CancellationTokenSource();
			token = _cancellation.Token;
		}
		OnChanged?.Invoke();

		_logger.LogInformation(
			"Barrier map started: table={Table} from={From} to={To} pip={PipSize} stops={Stops} targets={Targets} limits={Limits}m every={Spacing}s",
			normalized.Table,
			normalized.From,
			normalized.To,
			normalized.PipSize,
			normalized.StopPips,
			normalized.TargetPips,
			normalized.TimeLimitMinutes,
			normalized.EntrySpacingSeconds
		);

		_ = Task.Run(() => Run(normalized, token));
		return true;
	}

	public void Cancel()
	{
		lock (_lock)
			_cancellation?.Cancel();
	}

	private void Run(BarrierMapRequest request, CancellationToken token)
	{
		var stopwatch = Stopwatch.StartNew();
		try
		{
			var calculator = LoadTicks(request, token);
			var result = calculator.Compute(request, token, fraction => ReportProgress(LoadShare + (1 - LoadShare) * fraction));
			lock (_lock)
			{
				Result = result;
				Elapsed = stopwatch.Elapsed;
				ProgressPercent = 100;
			}
			_logger.LogInformation(
				"Barrier map finished: ticks={Ticks} entries={Entries} spread={Spread:0.##} pips elapsed={Elapsed}",
				result.TickCount,
				result.EntryCount,
				result.MeanSpreadPips,
				stopwatch.Elapsed
			);
		}
		catch (OperationCanceledException)
		{
			lock (_lock)
				WasCancelled = true;
			_logger.LogWarning("Barrier map cancelled after {Elapsed}", stopwatch.Elapsed);
		}
		catch (Exception ex)
		{
			lock (_lock)
				Error = ex.Message;
			_logger.LogError(ex, "Barrier map failed");
		}
		finally
		{
			lock (_lock)
			{
				IsRunning = false;
				_cancellation?.Dispose();
				_cancellation = null;
			}
			OnChanged?.Invoke();
		}
	}

	// The range is read past To by the longest time limit, so trades opened near the end can still run their course.
	private BarrierMapCalculator LoadTicks(BarrierMapRequest request, CancellationToken token)
	{
		var until = request.To.AddMinutes(request.TimeLimitMinutes.Max());
		var ticks = new List<long>();
		var bids = new List<float>();
		var asks = new List<float>();
		double span = Math.Max(1, (until - request.From).Ticks);

		foreach (var tick in _tickRepository.ReadTicks(request.Table, request.From, until))
		{
			ticks.Add(tick.Timestamp.Ticks);
			bids.Add((float)tick.Bid);
			asks.Add((float)tick.Ask);
			if (ticks.Count % LoadProgressTicks != 0)
				continue;
			token.ThrowIfCancellationRequested();
			ReportProgress(LoadShare * Math.Clamp((tick.Timestamp.Ticks - request.From.Ticks) / span, 0, 1));
		}
		return new BarrierMapCalculator(ticks.ToArray(), bids.ToArray(), asks.ToArray());
	}

	private void ReportProgress(double fraction)
	{
		lock (_lock)
			ProgressPercent = Math.Round(fraction * 100, 1);
		OnChanged?.Invoke();
	}
}
