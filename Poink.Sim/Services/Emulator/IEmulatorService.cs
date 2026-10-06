using System;

namespace Poink.Sim.Emulator;

/// <summary>
/// Drives the brokerage emulator: owns the synthetic asset's current price, order book and price history,
/// and steps them on a timer while running.
/// </summary>
public interface IEmulatorService
{
	EmulatorSettings Settings { get; }

	EmulatorSnapshot Snapshot { get; }

	bool IsRunning { get; }

	/// <summary>Raised after every step, start, stop and reset -- off the UI thread while running.</summary>
	event Action? Changed;

	void Start();

	void Stop();

	void Step();

	void Reset();
}
