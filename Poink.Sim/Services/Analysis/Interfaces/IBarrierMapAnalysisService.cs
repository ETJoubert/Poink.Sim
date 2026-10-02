using System;
using Poink.Sim.Models;

namespace Poink.Sim.Analysis;

/// <summary>
/// Runs barrier maps in the background: a trade every few seconds through a range, tested against a grid of
/// stop-loss and take-profit sizes and time limits. One run at a time.
/// </summary>
internal interface IBarrierMapAnalysisService
{
	bool IsRunning { get; }
	double ProgressPercent { get; }
	string? Error { get; }
	bool WasCancelled { get; }
	TimeSpan? Elapsed { get; }
	BarrierMapResult? Result { get; }

	event Action? OnChanged;

	bool TryStart(BarrierMapRequest request);

	void Cancel();
}
