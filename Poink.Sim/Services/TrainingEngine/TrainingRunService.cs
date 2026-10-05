using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Poink.Sim.Models.TrainingModels;
using Poink.Sim.Training;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// Owns TrainPageV2's training run -- its network, metrics and Start/Step/Pause/Stop/Reset -- so a run carries on in the
/// background when the page is left. The page attaches a <see cref="TrainingRunObserver"/> to drive the Live tab.
/// </summary>
internal sealed class TrainingRunService
{
	// Matches TrainingService's own windows (RecentRewardWindow/MaxMetricsLog), for consistent
	// behavior between the old and new training pages.
	private const int RecentRewardWindow = 20;
	private const int MaxRetainedEpisodes = 500;
	private static readonly TimeSpan PausePollInterval = TimeSpan.FromMilliseconds(200);

	private readonly TrainingEngineService _engine;
	private readonly ActionAssembler _actionAssembler;
	private readonly ILogger<TrainingRunService> _logger;

	private readonly List<RlEpisodeMetric> _episodeMetrics = [];
	private readonly Queue<double> _recentRewards = new();
	private readonly Queue<double> _recentEpisodeDurations = new();
	private CancellationTokenSource? _runCancellation;
	private volatile TrainingRunObserver? _observer;
	private volatile bool _isPaused;

	public TrainingRunService(TrainingEngineService engine, ActionAssembler actionAssembler, ILogger<TrainingRunService> logger)
	{
		_engine = engine;
		_actionAssembler = actionAssembler;
		_logger = logger;
	}

	public bool IsRunning { get; private set; }
	public bool IsPaused => _isPaused;
	public string? Error { get; private set; }
	public bool WasStopped { get; private set; }

	public MlpNetwork? Network { get; private set; }
	public ActionSpace? ActionSpace { get; private set; }

	/// <summary>Episode count captured when the network was built, so editing the options mid-run can't retarget progress.</summary>
	public int TargetEpisodeCount { get; private set; }

	/// <summary>Episodes a loaded model had already trained, which <see cref="EpisodesCompleted"/> doesn't include.</summary>
	public int LoadedEpisodesTrained { get; private set; }

	/// <summary>Atomically swapped, never mutated in place, so the page can render it while the run appends.</summary>
	public IReadOnlyList<RlEpisodeMetric> EpisodeMetrics { get; private set; } = [];

	public int EpisodesCompleted { get; private set; }
	public double BestReward { get; private set; }
	public double WorstReward { get; private set; }
	public double RecentAverageReward { get; private set; }
	public double EpisodesPerSecond { get; private set; }

	public bool IsViewingSavedResults { get; private set; }
	public string ViewedResultsName { get; private set; } = "";

	public bool IsTrainingInProgress => IsRunning && !IsPaused;

	public double ProgressPercent =>
		TargetEpisodeCount == 0 ? 0 : Math.Min(100.0, EpisodesCompleted * 100.0 / TargetEpisodeCount);

	public TrainingRunSnapshot CurrentRunSnapshot =>
		new(EpisodeMetrics, EpisodesCompleted, LoadedEpisodesTrained + EpisodesCompleted, RecentAverageReward, BestReward, WorstReward);

	/// <summary>Raised from any thread when the run starts, stops, pauses, resets or completes an episode.</summary>
	public event Action? Changed;

	public void Attach(TrainingRunObserver observer) => _observer = observer;

	public void Detach(TrainingRunObserver observer)
	{
		if (ReferenceEquals(_observer, observer))
			_observer = null;
	}

	public bool Start(TrainingOptionsModel options)
	{
		if (IsRunning)
			return false;

		ClearViewedSavedResults();
		IsRunning = true;
		_isPaused = false;
		Error = null;
		WasStopped = false;
		_runCancellation = new CancellationTokenSource();
		var token = _runCancellation.Token;
		_logger.LogInformation(
			"Training run started: table={Table}, episodes={Episodes}, strategy={Strategy}",
			options.Data.Table,
			options.Training.EpisodeTiming.EpisodeCount,
			options.Training.Learning.TrainingStrategyName
		);
		Changed?.Invoke();

		_ = Task.Run(() => RunTrainingLoopAsync(options, token));
		return true;
	}

	public async Task Step(TrainingOptionsModel options)
	{
		if (IsRunning && !IsPaused)
			return;

		try
		{
			ClearViewedSavedResults();
			EnsureNetwork(options);
			_runCancellation = new CancellationTokenSource();
			await RunOneEpisode(options, new Random(), _runCancellation.Token);
			Error = null;
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex)
		{
			Error = ex.Message;
			_logger.LogError(ex, "Training step failed");
		}
		Changed?.Invoke();
	}

	public void Pause()
	{
		_isPaused = true;
		Changed?.Invoke();
	}

	public void Resume()
	{
		_isPaused = false;
		Changed?.Invoke();
	}

	public void Stop()
	{
		WasStopped = true;
		_runCancellation?.Cancel();
		IsRunning = false;
		_isPaused = false;
		Changed?.Invoke();
	}

	public void Reset()
	{
		WasStopped = true;
		_runCancellation?.Cancel();
		IsRunning = false;
		_isPaused = false;
		Network = null;
		ActionSpace = null;
		TargetEpisodeCount = 0;
		LoadedEpisodesTrained = 0;
		ClearRunMetrics();
		Error = null;
		Changed?.Invoke();
	}

	/// <summary>Replaces the run with a saved model, ready to train further on the next Start.</summary>
	public void LoadModel(MlpNetwork network, ActionSpace actionSpace, int targetEpisodeCount, int episodesTrained)
	{
		Reset();
		Network = network;
		ActionSpace = actionSpace;
		TargetEpisodeCount = targetEpisodeCount;
		LoadedEpisodesTrained = episodesTrained;
		Changed?.Invoke();
	}

	/// <summary>Shows saved results in place of the run's metrics until the next Start, Step or Reset.</summary>
	public void ShowSavedResults(TrainingHistoryData data)
	{
		ClearRunMetrics();
		_episodeMetrics.AddRange(data.Episodes);
		EpisodeMetrics = _episodeMetrics.ToArray();
		EpisodesCompleted = data.Summary.EpisodesCompleted;
		BestReward = data.Summary.BestReward;
		WorstReward = data.Summary.WorstReward;
		RecentAverageReward = data.Summary.RecentAverageReward;
		IsViewingSavedResults = true;
		ViewedResultsName = data.Summary.Name;
		Changed?.Invoke();
	}

	private async Task RunTrainingLoopAsync(TrainingOptionsModel options, CancellationToken cancellationToken)
	{
		try
		{
			EnsureNetwork(options);

			var random = new Random();
			for (var i = 0; i < options.Training.EpisodeTiming.EpisodeCount; i++)
			{
				while (IsPaused)
				{
					if (cancellationToken.IsCancellationRequested)
						return;
					await Task.Delay(PausePollInterval, cancellationToken);
				}
				cancellationToken.ThrowIfCancellationRequested();

				await RunOneEpisode(options, random, cancellationToken);
				Changed?.Invoke();
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex)
		{
			Error = ex.Message;
			_logger.LogError(ex, "Training run failed after {Episodes} episodes", EpisodesCompleted);
		}
		finally
		{
			IsRunning = false;
			_isPaused = false;
			_logger.LogInformation(
				"Training run ended: episodes={Episodes}, stopped={Stopped}, failed={Failed}, best={Best:N2}, recentAvg={RecentAvg:N2}",
				EpisodesCompleted,
				WasStopped,
				Error is not null,
				BestReward,
				RecentAverageReward
			);
			Changed?.Invoke();
		}
	}

	private void EnsureNetwork(TrainingOptionsModel options)
	{
		if (Network is not null && ActionSpace is not null)
			return;

		TargetEpisodeCount = options.Training.EpisodeTiming.EpisodeCount;
		ActionSpace = _actionAssembler.Assemble(options.Output);
		Network = _engine.BuildNetwork(options, ActionSpace, new Random());
	}

	// Callbacks resolve the observer per call, so leaving the page mid-episode stops live updates straight away.
	private async Task RunOneEpisode(TrainingOptionsModel options, Random random, CancellationToken cancellationToken)
	{
		var observer = _observer;
		observer?.EpisodeStarting();
		var live = observer is not null && observer.IsLive();

		var stopwatch = Stopwatch.StartNew();
		var progress = TargetEpisodeCount > 1 ? (double)EpisodesCompleted / (TargetEpisodeCount - 1) : 1.0;
		var result = await _engine.RunEpisode(
			options,
			Network!,
			ActionSpace!,
			random,
			progress,
			live ? snapshot => _observer?.OnDecision(snapshot) : null,
			live ? tick => _observer?.OnTick(tick) : null,
			live ? () => _observer?.TickDelay() ?? TimeSpan.Zero : null,
			() => _observer?.IsActionPreviewWanted() ?? false,
			() => IsPaused,
			cancellationToken
		);
		stopwatch.Stop();
		RecordEpisode(result.Reward, result.FinalCapital.Equity, stopwatch.Elapsed);
	}

	private void RecordEpisode(double reward, double finalEquity, TimeSpan duration)
	{
		EpisodesCompleted++;
		BestReward = EpisodesCompleted == 1 ? reward : Math.Max(BestReward, reward);
		WorstReward = EpisodesCompleted == 1 ? reward : Math.Min(WorstReward, reward);

		_recentRewards.Enqueue(reward);
		if (_recentRewards.Count > RecentRewardWindow)
			_recentRewards.Dequeue();
		RecentAverageReward = _recentRewards.Average();

		_recentEpisodeDurations.Enqueue(duration.TotalSeconds);
		if (_recentEpisodeDurations.Count > RecentRewardWindow)
			_recentEpisodeDurations.Dequeue();
		var durationSum = _recentEpisodeDurations.Sum();
		EpisodesPerSecond = durationSum > 0 ? _recentEpisodeDurations.Count / durationSum : 0;

		_episodeMetrics.Add(new RlEpisodeMetric(EpisodesCompleted, reward, finalEquity));
		if (_episodeMetrics.Count > MaxRetainedEpisodes)
			_episodeMetrics.RemoveAt(0);
		EpisodeMetrics = _episodeMetrics.ToArray();
	}

	private void ClearRunMetrics()
	{
		_episodeMetrics.Clear();
		EpisodeMetrics = [];
		_recentRewards.Clear();
		_recentEpisodeDurations.Clear();
		EpisodesCompleted = 0;
		BestReward = 0;
		WorstReward = 0;
		RecentAverageReward = 0;
		EpisodesPerSecond = 0;
		IsViewingSavedResults = false;
	}

	private void ClearViewedSavedResults()
	{
		if (IsViewingSavedResults)
			ClearRunMetrics();
	}
}
