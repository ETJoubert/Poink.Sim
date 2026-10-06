using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Emulator;

namespace Poink.Sim.Models;

/// <summary>
/// What the Emulator page saves in the browser: the choices the user made (populations and their starting capital,
/// leverage, broker fund, order range, action frequency range, playback speed, chart view), never internal timings, so later default
/// changes still take effect. Choices added later are nullable so settings saved before them still load.
/// </summary>
public sealed record EmulatorPageSettings(
	IReadOnlyList<PopulationSettings> Populations,
	double Leverage,
	int SpeedMultiplier,
	int? OrderRangeTicks = null,
	double? ActivityChanceMin = null,
	double? ActivityChanceMax = null,
	EmulatorChartMode? ChartMode = null,
	double? BrokerFund = null
)
{
	/// <summary>The browser localStorage key the Emulator page keeps these settings under.</summary>
	public const string StorageKey = "poink.emulator.settings";

	public static EmulatorPageSettings From(EmulatorSettings settings, EmulatorChartMode chartMode) =>
		new(
			settings.Populations,
			settings.Leverage,
			settings.SpeedMultiplier,
			settings.OrderRangeTicks,
			settings.ActivityChanceMin,
			settings.ActivityChanceMax,
			chartMode,
			settings.BrokerFund
		);

	/// <summary>The saved choices laid over <paramref name="settings"/>; any saved value missing or no longer valid keeps the current one.</summary>
	public EmulatorSettings ApplyTo(EmulatorSettings settings)
	{
		var activityValid =
			ActivityChanceMin is >= 0 and <= 1 && ActivityChanceMax is >= 0 and <= 1 && ActivityChanceMin <= ActivityChanceMax;
		return settings with
		{
			Populations = Populations is { Count: > 0 } populations
				&& populations.All(p => !string.IsNullOrWhiteSpace(p.Name) && p.BotCount > 0 && p.StartingPoints > 0 && p.HasValidActionChances)
				&& populations.Select(p => p.Name).Distinct().Count() == populations.Count
					? populations.ToArray()
					: settings.Populations,
			Leverage = Leverage is >= EmulatorSettings.MinLeverage and <= EmulatorSettings.MaxLeverage ? Leverage : settings.Leverage,
			SpeedMultiplier = EmulatorSettings.SpeedMultipliers.Contains(SpeedMultiplier) ? SpeedMultiplier : settings.SpeedMultiplier,
			OrderRangeTicks = OrderRangeTicks is >= EmulatorSettings.MinOrderRangeTicks and <= EmulatorSettings.MaxOrderRangeTicks
				? OrderRangeTicks.Value
				: settings.OrderRangeTicks,
			ActivityChanceMin = activityValid ? ActivityChanceMin!.Value : settings.ActivityChanceMin,
			ActivityChanceMax = activityValid ? ActivityChanceMax!.Value : settings.ActivityChanceMax,
			BrokerFund = BrokerFund is >= EmulatorSettings.MinBrokerFund and <= EmulatorSettings.MaxBrokerFund
				? BrokerFund.Value
				: settings.BrokerFund,
		};
	}
}
