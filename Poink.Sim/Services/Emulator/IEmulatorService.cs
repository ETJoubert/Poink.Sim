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

	/// <summary>Market steps actually run per second while running -- below the requested speed when the CPU cannot keep up.</summary>
	double StepsPerSecond { get; }

	/// <summary>Raised after every step, start, stop and reset -- off the UI thread while running.</summary>
	event Action? Changed;

	void Start();

	void Stop();

	void Step();

	void Reset();

	/// <summary>Sets how many market steps each timer tick runs; must be one of <see cref="EmulatorSettings.SpeedMultipliers"/>.</summary>
	void SetSpeed(int speedMultiplier);

	/// <summary>
	/// Replaces the settings, keeping the market running if it was. Speed and leverage apply to the current market;
	/// any other change reopens it.
	/// </summary>
	void ApplySettings(EmulatorSettings settings);
}
