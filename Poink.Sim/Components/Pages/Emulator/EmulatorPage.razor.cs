using System;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Emulator;
using Poink.Sim.Helpers;

namespace Poink.Sim.Components.Pages
{
	/// <summary>
	/// Brokerage emulator: a synthetic asset's price, order book, bots and broker, driven by <see cref="IEmulatorService"/>,
	/// with a settings dialog that restarts the market on Apply.
	/// </summary>
	public partial class EmulatorPage : ComponentBase, IDisposable
	{
		[Inject]
		private IEmulatorService Emulator { get; set; } = default!;

		private bool _settingsOpen;

		private EmulatorSnapshot Snapshot => Emulator.Snapshot;

		protected override void OnInitialized()
		{
			Emulator.Changed += HandleEmulatorChanged;
		}

		private void OpenSettings() => _settingsOpen = true;

		private void CloseSettings() => _settingsOpen = false;

		private void ApplySettings(EmulatorSettings settings)
		{
			_settingsOpen = false;
			Emulator.ApplySettings(settings);
		}

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
