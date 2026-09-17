using System;
using Poink.Sim.Models;

namespace Poink.Sim.Analysis;

internal interface IPriceReturnAnalysisService
{
	bool IsRunning { get; }
	double ProgressPercent { get; }
	string? Error { get; }
	bool WasCancelled { get; }
	TimeSpan? Elapsed { get; }
	PriceReturnDistributionResult? Result { get; }

	event Action? OnChanged;

	bool TryStart(PriceReturnRequest request);

	void Cancel();
}
