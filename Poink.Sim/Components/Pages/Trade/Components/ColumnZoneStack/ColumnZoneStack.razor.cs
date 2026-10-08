using System;
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

		private const string TimeTickSplitStorageKey = "trade.quickConsole.timeTickSplit";

		private const string TimeTickDistanceStorageKey = "trade.quickConsole.timeTickDistance";

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

		private ColumnZoneReading? TimeTickReading =>
			ColumnZoneHelper.TimeTickReading(
				Grid,
				Settings.ColumnsBack,
				Settings.ZonePercent,
				Settings.TimeTickSplit,
				Settings.TimeTickDistance
			);

		private string TimeTickMeasure =>
			(Settings.TimeTickDistance ? "Time×Tk×Dist" : "Time×Tk") + (Settings.TimeTickSplit ? " split (up − down)" : string.Empty);

		private string TotalClass(ColumnZoneCell? total) =>
			total is null || !Settings.TimeTickSplit ? string.Empty
			: total.Value < 0 ? "czk-total-down"
			: "czk-total-up";

		private static string TotalHeat(ColumnZoneCell? total) =>
			(Math.Abs(total?.Intensity ?? 0) * 100).ToString("0.##", CultureInfo.InvariantCulture) + "%";

		private string TotalTooltip(ColumnZoneCell? total) =>
			total is null
				? $"Total {TimeTickMeasure} of columns {TimeTickColumns}: waiting for XGrid data"
				: Settings.TimeTickSplit
					? $"Net {TimeTickMeasure} over columns {TimeTickColumns}, {FormatPrice(total.Low)} - {FormatPrice(total.High)}: {ColumnZoneHelper.FormatValue(total.Value, signed: true)} ({FormatPercentValue(total.Intensity * 100)}% of the range's total up + down)"
					: $"Total {TimeTickMeasure} over columns {TimeTickColumns}, {FormatPrice(total.Low)} - {FormatPrice(total.High)}: {ColumnZoneHelper.FormatValue(total.Value, signed: false)}";

		private static string FormatCompact(double value)
		{
			var magnitude = Math.Abs(value);
			var (scaled, suffix) = magnitude >= 1e9 ? (magnitude / 1e9, "B")
				: magnitude >= 1e6 ? (magnitude / 1e6, "M")
				: magnitude >= 1e3 ? (magnitude / 1e3, "k")
				: (magnitude, string.Empty);
			var format = suffix.Length > 0 ? "0.#"
				: magnitude >= 1 || magnitude == 0 ? "0"
				: "G2";
			var text = scaled.ToString(format, CultureInfo.InvariantCulture) + suffix;
			return value < 0 ? "-" + text : text;
		}

		private static string FormatPrice(double price) => price.ToString("0.#####", CultureInfo.InvariantCulture);

		private static string FormatPercentValue(double percent) => percent.ToString("+0;-0;0", CultureInfo.InvariantCulture);

		private string TimeTickColumns =>
			Settings.ColumnsBack == 0 ? "n" : $"n to {ColumnZoneHelper.ColumnLabel(Settings.ColumnsBack)}";

		private ColumnZoneReading? ColumnReading(ColumnZoneRange? range) =>
			range is { } r ? ColumnZoneHelper.ColumnReading(r, Settings.ZonePercent) : null;

		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			if (!firstRender)
				return;

			var defaults = ColumnZoneHelper.DefaultSettings;
			var loaded = new ColumnZoneSettings(
				ColumnZoneHelper.ClampColumnsBack(await LocalStorage.GetItemAsync<int?>(ColumnsBackStorageKey) ?? defaults.ColumnsBack),
				ColumnZoneHelper.ClampZonePercent(await LocalStorage.GetItemAsync<double?>(ZonePercentStorageKey) ?? defaults.ZonePercent),
				await LocalStorage.GetItemAsync<bool?>(GateEnabledStorageKey) ?? defaults.GateEnabled,
				await LocalStorage.GetItemAsync<int?>(GateThresholdStorageKey) ?? defaults.GateThreshold,
				await LocalStorage.GetItemAsync<bool?>(TimeTickSplitStorageKey) ?? defaults.TimeTickSplit,
				await LocalStorage.GetItemAsync<bool?>(TimeTickDistanceStorageKey) ?? defaults.TimeTickDistance
			);
			Logger.LogInformation(
				"Column zone settings loaded: columns n to n-{ColumnsBack}, {ZonePercent}% zones, gate {GateState} at h = {GateThreshold}, Time×Tk split {SplitState}, distance {DistanceState}",
				loaded.ColumnsBack,
				loaded.ZonePercent,
				loaded.GateEnabled ? "on" : "off",
				loaded.GateThreshold,
				loaded.TimeTickSplit ? "on" : "off",
				loaded.TimeTickDistance ? "on" : "off"
			);
			await SettingsChanged.InvokeAsync(loaded);
		}

		private async Task ToggleTimeTickSplitAsync()
		{
			var split = !Settings.TimeTickSplit;
			Logger.LogInformation("Time×Tk zone slider split turned {SplitState}", split ? "on" : "off");
			await SettingsChanged.InvokeAsync(Settings with { TimeTickSplit = split });
			await LocalStorage.SetItemAsync(TimeTickSplitStorageKey, split);
		}

		private async Task ToggleTimeTickDistanceAsync()
		{
			var distance = !Settings.TimeTickDistance;
			Logger.LogInformation("Time×Tk zone slider distance multiplier turned {DistanceState}", distance ? "on" : "off");
			await SettingsChanged.InvokeAsync(Settings with { TimeTickDistance = distance });
			await LocalStorage.SetItemAsync(TimeTickDistanceStorageKey, distance);
		}

		private async Task SetColumnsBackAsync(double requested)
		{
			var columnsBack = ColumnZoneHelper.ClampColumnsBack(WholeOr(requested, Settings.ColumnsBack));
			Logger.LogInformation("Column zone sliders set to columns n to n-{ColumnsBack}", columnsBack);
			await SettingsChanged.InvokeAsync(Settings with { ColumnsBack = columnsBack });
			await LocalStorage.SetItemAsync(ColumnsBackStorageKey, columnsBack);
		}

		private async Task SetZonePercentAsync(double requested)
		{
			var zonePercent = ColumnZoneHelper.ClampZonePercent(double.IsFinite(requested) ? requested : Settings.ZonePercent);
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

		private async Task SetGateThresholdAsync(double requested)
		{
			var threshold = ColumnZoneHelper.ClampGateThreshold(WholeOr(requested, Settings.GateThreshold), Gate.ColumnCount);
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

		/// <summary>The stepper's value rounded to a whole count; a value it could not read keeps <paramref name="fallback"/>.</summary>
		private static int WholeOr(double value, int fallback) =>
			double.IsFinite(value) ? (int)Math.Clamp(Math.Round(value), int.MinValue, int.MaxValue) : fallback;
	}
}
