using System;
using Poink.Sim.Services.TrainingEngine;

namespace Poink.Sim.Services.Activity.Sources;

/// <summary>Reports the Train 2.0 page's tick-level training run, including while it is paused between episodes.</summary>
internal sealed class TrainingRunActivitySource : IActivitySource, IDisposable
{
	private readonly TrainingRunService _run;

	public TrainingRunActivitySource(TrainingRunService run)
	{
		_run = run;
		_run.Changed += RaiseChanged;
	}

	public string Name => "Training 2.0";
	public string Route => "/train2";

	public ActivityState Current =>
		_run.IsRunning ? new ActivityState(true, _run.TargetEpisodeCount > 0 ? _run.ProgressPercent : null) : ActivityState.Idle;

	public ActivityResult LastResult =>
		_run switch
		{
			{ Error: { } error } => new ActivityResult(ActivityOutcome.Failed, error),
			{ WasStopped: true } => new ActivityResult(ActivityOutcome.Stopped),
			_ => new ActivityResult(ActivityOutcome.Completed, $"{_run.EpisodesCompleted:N0} episodes, best reward {_run.BestReward:N2}"),
		};

	public event Action? Changed;

	private void RaiseChanged() => Changed?.Invoke();

	public void Dispose() => _run.Changed -= RaiseChanged;
}
