using System;
using Poink.Sim.SignalStudy;

namespace Poink.Sim.Services.Activity.Sources;

/// <summary>Reports the Signal Study page's dataset build; progress is unknown while it warms up.</summary>
internal sealed class SignalDatasetActivitySource : IActivitySource, IDisposable
{
	private readonly ISignalDatasetBuilder _builder;

	public SignalDatasetActivitySource(ISignalDatasetBuilder builder)
	{
		_builder = builder;
		_builder.OnChanged += RaiseChanged;
	}

	public string Name => "Signal dataset";
	public string Route => "/signal-study";

	public ActivityState Current =>
		_builder.IsBuilding
			? new ActivityState(true, _builder.Progress is { IsWarmingUp: false } p ? 100.0 * p.Fraction : null)
			: ActivityState.Idle;

	public ActivityResult LastResult =>
		_builder switch
		{
			{ WasCancelled: true } => new ActivityResult(ActivityOutcome.Stopped),
			{ LastError: { } error } => new ActivityResult(ActivityOutcome.Failed, error),
			{ Progress: { } progress } => new ActivityResult(ActivityOutcome.Completed, $"{progress.Name}: {progress.PointCount:N0} points"),
			_ => new ActivityResult(ActivityOutcome.Completed),
		};

	public event Action? Changed;

	private void RaiseChanged() => Changed?.Invoke();

	public void Dispose() => _builder.OnChanged -= RaiseChanged;
}
