using System;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Emulator;
using Poink.Sim.Helpers;

namespace Poink.Sim.Components.Pages
{
	/// <summary>
	/// Brokerage emulator: a synthetic asset's price, history and order book, driven by <see cref="IEmulatorService"/>.
	/// </summary>
	public partial class EmulatorPage : ComponentBase, IDisposable
	{
		[Inject]
		private IEmulatorService Emulator { get; set; } = default!;

		private EmulatorSnapshot Snapshot => Emulator.Snapshot;

		protected override void OnInitialized()
		{
			Emulator.Changed += HandleEmulatorChanged;
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
