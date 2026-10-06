using System;

namespace Poink.Sim.Emulator;

/// <summary>
/// Drives the brokerage emulator: a population of bots trading one synthetic asset through a broker's order book,
/// stepped on a timer while running. The price moves only when the broker fills orders.
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

	/// <summary>Replaces the settings and reopens the market with them, keeping it running if it was.</summary>
	void ApplySettings(EmulatorSettings settings);
}
