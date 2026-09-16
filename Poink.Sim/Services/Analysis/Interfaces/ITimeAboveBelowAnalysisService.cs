using System;
using Poink.Sim.Models;

namespace Poink.Sim.Analysis;

internal interface ITimeAboveBelowAnalysisService
{
	bool IsRunning { get; }
	double ProgressPercent { get; }
	string? Error { get; }
	bool WasCancelled { get; }
	TimeSpan? Elapsed { get; }
	TimeAboveBelowDistributionResult? Result { get; }

	event Action? OnChanged;

	bool TryStart(TimeAboveBelowRequest request);

	void Cancel();
}
