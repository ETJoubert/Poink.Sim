using System;
using Poink.Sim.Analysis;

namespace Poink.Sim.Services.Activity.Sources;

/// <summary>Reports one Analize page analysis; one instance is registered per analysis service.</summary>
internal sealed class AnalysisActivitySource : IActivitySource, IDisposable
{
	private readonly IBackgroundAnalysisService _analysis;

	public AnalysisActivitySource(string name, IBackgroundAnalysisService analysis)
	{
		Name = name;
		_analysis = analysis;
		_analysis.OnChanged += RaiseChanged;
	}

	public string Name { get; }
	public string Route => "/analize";

	public ActivityState Current =>
		_analysis.IsRunning ? new ActivityState(true, _analysis.ProgressPercent) : ActivityState.Idle;

	public ActivityResult LastResult =>
		_analysis switch
		{
			{ Error: { } error } => new ActivityResult(ActivityOutcome.Failed, error),
			{ WasCancelled: true } => new ActivityResult(ActivityOutcome.Stopped),
			{ Elapsed: { } elapsed } => new ActivityResult(ActivityOutcome.Completed, $"Took {elapsed:hh\\:mm\\:ss}"),
			_ => new ActivityResult(ActivityOutcome.Completed),
		};

	public event Action? Changed;

	private void RaiseChanged() => Changed?.Invoke();

	public void Dispose() => _analysis.OnChanged -= RaiseChanged;
}
