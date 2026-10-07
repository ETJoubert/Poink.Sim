using System;

namespace Poink.Sim.Models;

/// <summary>Average ticks per second over Window, ending at the newest tick (AsOfUtc).</summary>
public readonly record struct TickRate(
	double TicksPerSecond,
	double UpPerSecond,
	double DownPerSecond,
	TimeSpan Window,
	DateTime AsOfUtc
)
{
	public double DeltaPerSecond => UpPerSecond - DownPerSecond;
}
