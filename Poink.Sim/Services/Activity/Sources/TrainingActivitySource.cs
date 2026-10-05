using System;
using Poink.Sim.Training;

namespace Poink.Sim.Services.Activity.Sources;

/// <summary>Reports the Train page's RL training run, including while it is paused between episodes.</summary>
internal sealed class TrainingActivitySource : IActivitySource, IDisposable
{
	private readonly ITrainingService _training;

	public TrainingActivitySource(ITrainingService training)
	{
		_training = training;
		_training.OnChanged += RaiseChanged;
	}

	public string Name => "Training";
	public string Route => "/train";

	public ActivityState Current =>
		_training.IsRunning
			? new ActivityState(true, _training.TotalEpisodes > 0 ? 100.0 * _training.EpisodesCompleted / _training.TotalEpisodes : null)
			: ActivityState.Idle;

	public ActivityResult LastResult
	{
		get
		{
			if (_training.Error is { } error)
				return new ActivityResult(ActivityOutcome.Failed, error);
			return _training.EpisodesCompleted >= _training.TotalEpisodes
				? new ActivityResult(
					ActivityOutcome.Completed,
					$"{_training.EpisodesCompleted:N0} episodes, best reward {_training.BestReward:N2}"
				)
				: new ActivityResult(ActivityOutcome.Stopped);
		}
	}

	public event Action? Changed;

	private void RaiseChanged() => Changed?.Invoke();

	public void Dispose() => _training.OnChanged -= RaiseChanged;
}
