using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Poink.Sim.Emulator;
using Poink.Sim.Helpers;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Pages
{
	/// <summary>
	/// Brokerage emulator: a synthetic asset's price, order book, bots and broker, driven by <see cref="IEmulatorService"/>.
	/// The user's settings and playback speed are saved in the browser and restored when the page first renders.
	/// </summary>
	public partial class EmulatorPage : ComponentBase, IDisposable
	{
		[Inject]
		private IEmulatorService Emulator { get; set; } = default!;

		[Inject]
		private ILocalStorageHelper LocalStorage { get; set; } = default!;

		[Inject]
		private ILogger<EmulatorPage> Logger { get; set; } = default!;

		private const int TicksPerChartBar = 10;

		private bool _settingsLoaded;
		private EmulatorChartMode _chartMode = EmulatorChartMode.Line;

		private EmulatorSnapshot Snapshot => Emulator.Snapshot;

		private TimeSpan BarSpan => Emulator.Settings.TickInterval * TicksPerChartBar;

		protected override void OnInitialized()
		{
			Emulator.Changed += HandleEmulatorChanged;
		}

		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			if (firstRender)
			{
				await LoadSavedSettingsAsync();
			}
		}

		private Task ApplySettingsAsync(EmulatorSettings settings)
		{
			Emulator.ApplySettings(settings);
			return SaveSettingsAsync();
		}

		private IReadOnlyList<BotSummary> BotsOf(string group) => Snapshot.Bots.Where(bot => bot.Population == group).ToArray();

		private IReadOnlyCollection<string> OtherGroupNames(int index) =>
			Emulator.Settings.Populations.Where((_, i) => i != index).Select(group => group.Name).ToArray();

		private Task ChangeGroupAsync(int index, PopulationSettings group) =>
			ApplyGroupsAsync(Emulator.Settings.Populations.Select((existing, i) => i == index ? group : existing));

		private Task RemoveGroupAsync(int index) =>
			ApplyGroupsAsync(Emulator.Settings.Populations.Where((_, i) => i != index));

		private Task AddGroupAsync()
		{
			var groups = Emulator.Settings.Populations;
			if (groups.Count >= EmulatorSettings.MaxGroups)
			{
				return Task.CompletedTask;
			}

			var number = groups.Count + 1;
			while (groups.Any(group => group.Name == $"Group {number}"))
			{
				number++;
			}

			return ApplyGroupsAsync(groups.Append(groups[^1] with { Name = $"Group {number}" }));
		}

		private Task ApplyGroupsAsync(IEnumerable<PopulationSettings> groups) =>
			ApplySettingsAsync(Emulator.Settings with { Populations = groups.ToArray() });

		private Task SetChartModeAsync(EmulatorChartMode mode)
		{
			_chartMode = mode;
			return SaveSettingsAsync();
		}

		private Task SetSpeedAsync(int speedMultiplier)
		{
			Emulator.SetSpeed(speedMultiplier);
			return SaveSettingsAsync();
		}

		// Restored after the first render because browser storage is only reachable over JS interop once rendered.
		private async Task LoadSavedSettingsAsync()
		{
			try
			{
				var saved = await LocalStorage.GetItemAsync<EmulatorPageSettings>(EmulatorPageSettings.StorageKey);
				if (saved is null)
				{
					return;
				}

				_chartMode = saved.ChartMode ?? _chartMode;
				var settings = saved.ApplyTo(Emulator.Settings);
				if (!SameChoices(Emulator.Settings, settings))
				{
					Emulator.ApplySettings(settings);
				}
				StateHasChanged();
				Logger.LogInformation("Emulator settings loaded from browser storage: {Settings}", saved);
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, "Saved Emulator settings could not be loaded; keeping the current settings");
			}
			finally
			{
				_settingsLoaded = true;
			}
		}

		// Saving before the stored settings were read would overwrite them with the defaults.
		private async Task SaveSettingsAsync()
		{
			if (!_settingsLoaded)
			{
				return;
			}

			var settings = EmulatorPageSettings.From(Emulator.Settings, _chartMode);
			try
			{
				await LocalStorage.SetItemAsync(EmulatorPageSettings.StorageKey, settings);
				Logger.LogInformation("Emulator settings saved to browser storage: {Settings}", settings);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "Saving Emulator settings to browser storage failed");
			}
		}

		private static bool SameChoices(EmulatorSettings current, EmulatorSettings next) =>
			current.Populations.SequenceEqual(next.Populations)
			&& current.Leverage == next.Leverage
			&& current.BrokerFund == next.BrokerFund
			&& current.SpeedMultiplier == next.SpeedMultiplier
			&& current.OrderRangeTicks == next.OrderRangeTicks
			&& !current.ActivityRangeDiffers(next);

		private void HandleEmulatorChanged() => _ = InvokeAsync(StateHasChanged);

		private string FormatPrice(double? price) =>
			price is { } value
				? value.ToString(EmulatorChartHelper.PriceFormat(Emulator.Settings.TickSize), CultureInfo.InvariantCulture)
				: "--";

		public void Dispose()
		{
			Emulator.Changed -= HandleEmulatorChanged;
		}
	}
}
