using System;
using Poink.Sim.Simulation;

namespace Poink.Sim.Services.Activity.Sources;

/// <summary>Reports simulation playback; a pause is silent, while reaching the end or a depletion stop counts as completed.</summary>
internal sealed class SimulationActivitySource : IActivitySource, IDisposable
{
	private readonly ISimulationEngine _engine;

	public SimulationActivitySource(ISimulationEngine engine)
	{
		_engine = engine;
		_engine.OnChanged += RaiseChanged;
		_engine.OnProgress += RaiseChanged;
	}

	public string Name => "Simulation";
	public string Route => "/";

	public ActivityState Current
	{
		get
		{
			if (_engine.Snapshot is not { IsPlaying: true })
				return ActivityState.Idle;
			var progress = _engine.Progress;
			return new ActivityState(true, progress is { TotalCount: > 0 } ? 100.0 * progress.CursorIndex / progress.TotalCount : null);
		}
	}

	public ActivityResult LastResult =>
		_engine.Snapshot switch
		{
			{ StopReason: { } reason } => new ActivityResult(ActivityOutcome.Completed, reason),
			{ IsFinished: true } snapshot => new ActivityResult(ActivityOutcome.Completed, $"Replayed {snapshot.TotalCount:N0} ticks"),
			_ => new ActivityResult(ActivityOutcome.Paused),
		};

	public event Action? Changed;

	private void RaiseChanged() => Changed?.Invoke();

	public void Dispose()
	{
		_engine.OnChanged -= RaiseChanged;
		_engine.OnProgress -= RaiseChanged;
	}
}
