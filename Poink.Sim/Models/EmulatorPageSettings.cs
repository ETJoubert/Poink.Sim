using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Emulator;

namespace Poink.Sim.Models;

/// <summary>
/// What the Emulator page saves in the browser: the choices the user made (populations and their starting capital,
/// leverage, playback speed), never internal timings, so later default changes still take effect.
/// </summary>
public sealed record EmulatorPageSettings(IReadOnlyList<PopulationSettings> Populations, double Leverage, int SpeedMultiplier)
{
	/// <summary>The browser localStorage key the Emulator page keeps these settings under.</summary>
	public const string StorageKey = "poink.emulator.settings";

	public static EmulatorPageSettings From(EmulatorSettings settings) =>
		new(settings.Populations, settings.Leverage, settings.SpeedMultiplier);

	/// <summary>The saved choices laid over <paramref name="settings"/>; any saved value no longer valid keeps the current one.</summary>
	public EmulatorSettings ApplyTo(EmulatorSettings settings) =>
		settings with
		{
			Populations = Populations is { Count: > 0 } populations && populations.All(p => p.BotCount > 0 && p.StartingPoints > 0)
				? populations.ToArray()
				: settings.Populations,
			Leverage = Leverage is >= EmulatorSettings.MinLeverage and <= EmulatorSettings.MaxLeverage ? Leverage : settings.Leverage,
			SpeedMultiplier = EmulatorSettings.SpeedMultipliers.Contains(SpeedMultiplier) ? SpeedMultiplier : settings.SpeedMultiplier,
		};
}
