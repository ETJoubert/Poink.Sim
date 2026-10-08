using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Poink.Sim.Components.Pages.Trade.Helpers;
using Poink.Sim.Components.Pages.Trade.Models;
using Poink.Sim.Helpers;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>
	/// The quick console's automation toggles: auto-arm after t, a market time stop after t + s, and auto-fire on a zone
	/// gate pass. t, s and the two close toggles are kept per browser; auto-fire always starts off, like ARMED.
	/// </summary>
	public partial class QuickTradeAutomation : ComponentBase
	{
		private const int SecondsStep = 5;

		private const string AutoArmStorageKey = "trade.quickConsole.autoArm";

		private const string ArmAfterStorageKey = "trade.quickConsole.armAfterSeconds";

		private const string AutoCloseStorageKey = "trade.quickConsole.autoClose";

		private const string CloseAfterStorageKey = "trade.quickConsole.closeAfterSeconds";

		[Inject]
		private ILocalStorageHelper LocalStorage { get; set; } = null!;

		[Inject]
		private ILogger<QuickTradeAutomation> Logger { get; set; } = null!;

		[Parameter]
		public CloseAutomationSettings Settings { get; set; } = PositionCloseHelper.DefaultAutomation;

		[Parameter]
		public EventCallback<CloseAutomationSettings> SettingsChanged { get; set; }

		[Parameter]
		public bool AutoFire { get; set; }

		[Parameter]
		public EventCallback<bool> AutoFireChanged { get; set; }

		/// <summary>Auto-fire needs the zone gate on to have anything to react to.</summary>
		[Parameter]
		public bool GateEnabled { get; set; }

		private string AutoFireTooltip =>
			AutoFire ? "Auto-fire on: one market order of the selected volume when a side's zone gate goes from locked to open (flat side, ARMED only) -- click to turn off"
			: GateEnabled ? "Auto-fire: place one market order of the selected volume when a side's zone gate goes from locked to open, only on a flat side and only while ARMED"
			: "Turn the zone gate on to use auto-fire";

		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			if (!firstRender)
				return;

			var defaults = PositionCloseHelper.DefaultAutomation;
			var loaded = new CloseAutomationSettings(
				await LocalStorage.GetItemAsync<bool?>(AutoArmStorageKey) ?? defaults.AutoArm,
				PositionCloseHelper.ClampSeconds(await LocalStorage.GetItemAsync<int?>(ArmAfterStorageKey) ?? defaults.ArmAfterSeconds),
				await LocalStorage.GetItemAsync<bool?>(AutoCloseStorageKey) ?? defaults.AutoClose,
				PositionCloseHelper.ClampSeconds(await LocalStorage.GetItemAsync<int?>(CloseAfterStorageKey) ?? defaults.CloseAfterSeconds)
			);
			Logger.LogInformation(
				"Quick console automation loaded: auto-arm {AutoArm} after {ArmAfterSeconds}s, time stop {AutoClose} after {TimeStopSeconds}s",
				loaded.AutoArm ? "on" : "off",
				loaded.ArmAfterSeconds,
				loaded.AutoClose ? "on" : "off",
				loaded.TimeStopSeconds
			);
			await SettingsChanged.InvokeAsync(loaded);
		}

		private async Task ToggleAutoArmAsync()
		{
			var autoArm = !Settings.AutoArm;
			Logger.LogInformation("Auto-arm turned {State}", autoArm ? "on" : "off");
			await SettingsChanged.InvokeAsync(Settings with { AutoArm = autoArm });
			await LocalStorage.SetItemAsync(AutoArmStorageKey, autoArm);
		}

		private async Task SetArmAfterSecondsAsync(double requested)
		{
			var seconds = PositionCloseHelper.ClampSeconds(WholeOr(requested, Settings.ArmAfterSeconds));
			Logger.LogInformation("Auto-arm delay t set to {Seconds}s", seconds);
			await SettingsChanged.InvokeAsync(Settings with { ArmAfterSeconds = seconds });
			await LocalStorage.SetItemAsync(ArmAfterStorageKey, seconds);
		}

		private async Task ToggleAutoCloseAsync()
		{
			var autoClose = !Settings.AutoClose;
			Logger.LogInformation("Time stop turned {State}", autoClose ? "on" : "off");
			await SettingsChanged.InvokeAsync(Settings with { AutoClose = autoClose });
			await LocalStorage.SetItemAsync(AutoCloseStorageKey, autoClose);
		}

		private async Task SetCloseAfterSecondsAsync(double requested)
		{
			var seconds = PositionCloseHelper.ClampSeconds(WholeOr(requested, Settings.CloseAfterSeconds));
			Logger.LogInformation("Time stop extra s set to {Seconds}s", seconds);
			await SettingsChanged.InvokeAsync(Settings with { CloseAfterSeconds = seconds });
			await LocalStorage.SetItemAsync(CloseAfterStorageKey, seconds);
		}

		private async Task ToggleAutoFireAsync()
		{
			var autoFire = !AutoFire;
			Logger.LogInformation("Auto-fire on zone gate pass turned {State}", autoFire ? "on" : "off");
			await AutoFireChanged.InvokeAsync(autoFire);
		}

		/// <summary>The stepper's value rounded to whole seconds; a value it could not read keeps <paramref name="fallback"/>.</summary>
		private static int WholeOr(double value, int fallback) =>
			double.IsFinite(value) ? (int)Math.Clamp(Math.Round(value), int.MinValue, int.MaxValue) : fallback;
	}
}
