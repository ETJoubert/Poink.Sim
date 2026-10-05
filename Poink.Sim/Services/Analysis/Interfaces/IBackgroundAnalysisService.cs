using System;

namespace Poink.Sim.Analysis;

/// <summary>
/// Run state every background analysis shares: one run at a time, reporting progress and how its last run ended.
/// </summary>
internal interface IBackgroundAnalysisService
{
	bool IsRunning { get; }
	double ProgressPercent { get; }
	string? Error { get; }
	bool WasCancelled { get; }
	TimeSpan? Elapsed { get; }

	event Action? OnChanged;

	void Cancel();
}
