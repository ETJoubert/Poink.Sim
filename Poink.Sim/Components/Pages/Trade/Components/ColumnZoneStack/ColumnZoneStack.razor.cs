using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Poink.Sim.Components.Charts;
using Poink.Sim.Components.Pages.Trade.Helpers;
using Poink.Sim.Components.Pages.Trade.Models;
using Poink.Sim.Helpers;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>
	/// One <see cref="ColumnZoneSlider"/> per XGrid column from the latest (n) back to n-k, plus the zone gate's controls.
	/// The settings are bound by the owner, which applies the gate; this component edits them and keeps them per browser.
	/// </summary>
	public partial class ColumnZoneStack : ComponentBase
	{
		private const string ColumnsBackStorageKey = "trade.quickConsole.columnsBack";

		private const string ZonePercentStorageKey = "trade.quickConsole.columnZonePercent";

		private const string GateEnabledStorageKey = "trade.quickConsole.zoneGateEnabled";

		private const string GateThresholdStorageKey = "trade.quickConsole.zoneGateThreshold";

		[Inject]
		private ILocalStorageHelper LocalStorage { get; set; } = null!;

		[Inject]
		private ILogger<ColumnZoneStack> Logger { get; set; } = null!;

		/// <summary>The charted symbol's XGrid; null while the chart has none.</summary>
		[Parameter]
		public XGridResult? Grid { get; set; }

		[Parameter]
		public ColumnZoneSettings Settings { get; set; } = ColumnZoneHelper.DefaultSettings;

		[Parameter]
		public EventCallback<ColumnZoneSettings> SettingsChanged { get; set; }

		private IReadOnlyList<ColumnZoneRange?> Ranges => ColumnZoneHelper.RecentColumns(Grid, Settings.ColumnsBack);

		private ColumnZoneGate Gate => ColumnZoneHelper.Gate(Grid, Settings);

		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			if (!firstRender)
				return;

			var defaults = ColumnZoneHelper.DefaultSettings;
			var loaded = new ColumnZoneSettings(
				ColumnZoneHelper.ClampColumnsBack(await LocalStorage.GetItemAsync<int?>(ColumnsBackStorageKey) ?? defaults.ColumnsBack),
				ColumnZoneHelper.ClampZonePercent(await LocalStorage.GetItemAsync<double?>(ZonePercentStorageKey) ?? defaults.ZonePercent),
				await LocalStorage.GetItemAsync<bool?>(GateEnabledStorageKey) ?? defaults.GateEnabled,
				await LocalStorage.GetItemAsync<int?>(GateThresholdStorageKey) ?? defaults.GateThreshold
			);
			Logger.LogInformation(
				"Column zone settings loaded: columns n to n-{ColumnsBack}, {ZonePercent}% zones, gate {GateState} at h = {GateThreshold}",
				loaded.ColumnsBack,
				loaded.ZonePercent,
				loaded.GateEnabled ? "on" : "off",
				loaded.GateThreshold
			);
			await SettingsChanged.InvokeAsync(loaded);
		}

		private async Task SetColumnsBackAsync(ChangeEventArgs args)
		{
			var columnsBack = ColumnZoneHelper.ClampColumnsBack(ParseInt(args, Settings.ColumnsBack));
			Logger.LogInformation("Column zone sliders set to columns n to n-{ColumnsBack}", columnsBack);
			await SettingsChanged.InvokeAsync(Settings with { ColumnsBack = columnsBack });
			await LocalStorage.SetItemAsync(ColumnsBackStorageKey, columnsBack);
		}

		private async Task SetZonePercentAsync(ChangeEventArgs args)
		{
			var requested = double.TryParse(args.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
				? parsed
				: Settings.ZonePercent;
			var zonePercent = ColumnZoneHelper.ClampZonePercent(requested);
			Logger.LogInformation("Column zone share set to {ZonePercent}%", zonePercent);
			await SettingsChanged.InvokeAsync(Settings with { ZonePercent = zonePercent });
			await LocalStorage.SetItemAsync(ZonePercentStorageKey, zonePercent);
		}

		private async Task ToggleGateAsync()
		{
			var enabled = !Settings.GateEnabled;
			Logger.LogInformation("Column zone gate turned {GateState}", enabled ? "on" : "off");
			await SettingsChanged.InvokeAsync(Settings with { GateEnabled = enabled });
			await LocalStorage.SetItemAsync(GateEnabledStorageKey, enabled);
		}

		private async Task SetGateThresholdAsync(ChangeEventArgs args)
		{
			var threshold = ColumnZoneHelper.ClampGateThreshold(ParseInt(args, Settings.GateThreshold), Gate.ColumnCount);
			Logger.LogInformation("Column zone gate threshold set to h = {GateThreshold}", threshold);
			await SettingsChanged.InvokeAsync(Settings with { GateThreshold = threshold });
			await LocalStorage.SetItemAsync(GateThresholdStorageKey, threshold);
		}

		private string GateSideTooltip(bool isLong)
		{
			var gate = Gate;
			var side = isLong ? "LONG" : "SHORT";
			var opensFrom = isLong ? "LOW/BELOW" : "HIGH/ABOVE";
			var state = gate.IsLocked(isLong) ? "locked" : "open";
			return $"{side} {state}: {gate.Supporters(isLong)} of {gate.ColumnCount} columns are {opensFrom} (needs {gate.Threshold})";
		}

		private static int ParseInt(ChangeEventArgs args, int fallback) =>
			int.TryParse(args.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;

		private static string FormatPercent(double percent) => percent.ToString("0", CultureInfo.InvariantCulture);
	}
}
