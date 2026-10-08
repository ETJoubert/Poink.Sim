using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
	/// One-click market trading for the charted symbol: a volume and a LONG and a SHORT button. A side's button turns
	/// into CLOSE while that side has open positions on the symbol. The page owns the positions and executes requests.
	/// </summary>
	public partial class QuickTradeConsole : ComponentBase
	{
		private const string VolumeStorageKey = "trade.quickConsole.volume";

		[Inject]
		private ILocalStorageHelper LocalStorage { get; set; } = null!;

		[Inject]
		private ILogger<QuickTradeConsole> Logger { get; set; } = null!;

		[Inject]
		private TimeProvider Clock { get; set; } = null!;

		[Parameter]
		public bool HasSelection { get; set; }

		[Parameter]
		public string? SymbolName { get; set; }

		[Parameter, EditorRequired]
		public QuickTradeSideState Long { get; set; } = null!;

		[Parameter, EditorRequired]
		public QuickTradeSideState Short { get; set; } = null!;

		[Parameter]
		public double? MinVolume { get; set; }

		[Parameter]
		public double? VolumeStep { get; set; }

		[Parameter]
		public double? CurrentBid { get; set; }

		[Parameter]
		public double? CurrentAsk { get; set; }

		[Parameter]
		public double? Leverage { get; set; }

		/// <summary>The account's free margin before this order; null while it cannot be priced.</summary>
		[Parameter]
		public double? AvailableMargin { get; set; }

		/// <summary>The charted symbol's XGrid, read by the column zone sliders; null while the chart has none.</summary>
		[Parameter]
		public XGridResult? ChartXGrid { get; set; }

		[Parameter]
		public string? StatusMessage { get; set; }

		[Parameter]
		public bool StatusIsError { get; set; }

		[Parameter]
		public EventCallback<QuickTradeOpenRequest> OnOpen { get; set; }

		[Parameter]
		public EventCallback<bool> OnClose { get; set; }

		/// <summary>Arms or disarms a side's close-at-breakeven; the bool is the side (true = long).</summary>
		[Parameter]
		public EventCallback<bool> OnToggleBreakeven { get; set; }

		/// <summary>Raised with the auto-arm and time-stop settings whenever they load or change, for the page to apply.</summary>
		[Parameter]
		public EventCallback<CloseAutomationSettings> OnAutomationChanged { get; set; }

		private CloseAutomationSettings _automation = PositionCloseHelper.DefaultAutomation;

		private bool _autoFire;

		// Each side's gate lock as of the last render, so auto-fire reacts only to a locked-to-open change.
		private readonly Dictionary<bool, bool> _gateWasLocked = new();

		private string? _gateSymbol;

		// A bar's start point is fixed when it first appears; re-setting a running CSS animation's delay would make it jump.
		private readonly Dictionary<string, double> _countdownStarts = new();

		private readonly HashSet<string> _countdownsShown = new();

		private sealed record CountdownBar(string Key, string Kind, string Style, string Tooltip);

		private double? _storedVolume;

		private bool _armed;

		private ColumnZoneSettings _zoneSettings = ColumnZoneHelper.DefaultSettings;

		private ColumnZoneGate ZoneGate => ColumnZoneHelper.Gate(ChartXGrid, _zoneSettings);

		private double? SelectedVolume =>
			QuickTradeHelper.ChooseVolume(_storedVolume, QuickTradeHelper.VolumePresets, MinVolume);

		private double OrderVolume =>
			SelectedVolume is { } volume ? QuickTradeHelper.SnapVolume(volume, MinVolume, VolumeStep) : 0;

		private double? OrderMargin =>
			SelectedVolume is null ? null : QuickTradeHelper.OrderMargin(OrderVolume, CurrentBid, CurrentAsk, Leverage);

		private double? MarginFreeAfter => QuickTradeHelper.MarginFreeAfter(AvailableMargin, OrderMargin);

		private string Symbol => SymbolName ?? "the charted symbol";

		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			if (firstRender)
			{
				_storedVolume = await LocalStorage.GetItemAsync<double?>(VolumeStorageKey);
				StateHasChanged();
			}

			_countdownStarts.Keys.Where(key => !_countdownsShown.Contains(key)).ToList().ForEach(key => _countdownStarts.Remove(key));
			_countdownsShown.Clear();

			await AutoFireOnGatePassAsync();
		}

		private IEnumerable<CountdownBar> CountdownBars(QuickTradeSideState side)
		{
			if (side.OldestOpenUtc is not { } opened)
				yield break;

			var nowUtc = Clock.GetUtcNow().UtcDateTime;
			var oldest = side.OpenCount > 1 ? $" (oldest of {side.OpenCount})" : string.Empty;
			if (_automation.AutoArm && !side.BreakevenArmed)
			{
				var arm = PositionCloseHelper.Countdown(opened, nowUtc, _automation.ArmAfterSeconds);
				var tooltip = arm.IsDone
					? $"Auto BE watching: the position{oldest} has been open past t = {_automation.ArmAfterSeconds}s"
					: $"Auto BE joins the breakeven watch in {arm.RemainingSeconds:0}s{oldest}";
				yield return Bar(side, "arm", opened, arm, tooltip);
			}

			if (_automation.AutoClose)
			{
				var stop = PositionCloseHelper.Countdown(opened, nowUtc, _automation.TimeStopSeconds);
				yield return Bar(side, "stop", opened, stop, $"Time stop closes at market in {stop.RemainingSeconds:0}s{oldest} (t + s = {_automation.TimeStopSeconds}s)");
			}
		}

		private CountdownBar Bar(QuickTradeSideState side, string kind, DateTime opened, CloseCountdown countdown, string tooltip)
		{
			var key = $"{(side.IsLong ? "long" : "short")}-{kind}-{opened.Ticks}-{countdown.TotalSeconds}";
			_countdownsShown.Add(key);
			if (!_countdownStarts.TryGetValue(key, out var startSeconds))
				_countdownStarts[key] = startSeconds = countdown.ElapsedSeconds;

			var style = string.Create(
				CultureInfo.InvariantCulture,
				$"animation-duration: {countdown.TotalSeconds}s; animation-delay: -{startSeconds:0.###}s"
			);
			return new CountdownBar(key, kind, style, tooltip);
		}

		private async Task SetAutomationAsync(CloseAutomationSettings settings)
		{
			_automation = settings;
			await OnAutomationChanged.InvokeAsync(settings);
		}

		private async Task AutoFireOnGatePassAsync()
		{
			var gate = ZoneGate;
			var symbolChanged = _gateSymbol != SymbolName;
			_gateSymbol = SymbolName;
			foreach (var side in new[] { Long, Short })
			{
				var wasLocked = !symbolChanged && _gateWasLocked.TryGetValue(side.IsLong, out var locked) && locked;
				_gateWasLocked[side.IsLong] = gate.IsLocked(side.IsLong);
				if (!QuickTradeHelper.ShouldAutoFire(_autoFire && _armed && HasSelection, wasLocked, gate, side, OrderVolume))
					continue;

				Logger.LogInformation(
					"Auto-fire: {Side} zone gate passed on {Symbol} ({Supporters} of {Columns} columns, needs {Threshold}); placing a market order of {Volume}",
					SideName(side),
					Symbol,
					gate.Supporters(side.IsLong),
					gate.ColumnCount,
					gate.Threshold,
					OrderVolume
				);
				await OnOpen.InvokeAsync(new QuickTradeOpenRequest(side.IsLong, OrderVolume));
			}
		}

		private Task SelectVolumeAsync(double volume)
		{
			_storedVolume = volume;
			return LocalStorage.SetItemAsync(VolumeStorageKey, volume);
		}

		private bool IsSelected(double volume) => SelectedVolume == volume;

		private string VolumeTooltip(double volume) =>
			QuickTradeHelper.IsVolumeAllowed(volume, MinVolume)
				? $"Trade {FormatVolume(volume)} units per order"
				: $"{Symbol} needs at least {FormatVolume(MinVolume ?? 0)} units per order";

		private void ToggleArmed() => _armed = !_armed;

		private bool CanPress(QuickTradeSideState side) =>
			HasSelection && !side.Busy && (side.HasOpenPositions || (_armed && OrderVolume > 0 && !IsZoneLocked(side)));

		private bool IsZoneLocked(QuickTradeSideState side) => ZoneGate.IsLocked(side.IsLong);

		private Task PressAsync(QuickTradeSideState side) =>
			side.HasOpenPositions
				? OnClose.InvokeAsync(side.IsLong)
				: OnOpen.InvokeAsync(new QuickTradeOpenRequest(side.IsLong, OrderVolume));

		private static string SideName(QuickTradeSideState side) => side.IsLong ? "LONG" : "SHORT";

		private static string SideLabel(QuickTradeSideState side) =>
			side.Busy ? "WORKING..."
			: side.HasOpenPositions ? $"CLOSE {SideName(side)}"
			: SideName(side);

		private string SideDetail(QuickTradeSideState side) =>
			side.HasOpenPositions ? $"{FormatVolume(side.OpenVolume)} open"
			: IsZoneLocked(side) ? "zone locked"
			: $"{FormatVolume(OrderVolume)} @ market";

		private static string SideClass(QuickTradeSideState side) =>
			(side.IsLong ? "qtc-btn-long" : "qtc-btn-short")
			+ (side.HasOpenPositions ? " qtc-btn-close" : string.Empty);

		private string SideTooltip(QuickTradeSideState side)
		{
			if (!HasSelection)
				return "Open a chart to trade its symbol";
			if (side.HasOpenPositions)
				return $"Close all {side.OpenCount} {SideName(side).ToLowerInvariant()} position(s) on {Symbol} ({FormatVolume(side.OpenVolume)} units)";
			if (!_armed)
				return "Locked -- arm the guard switch above the volume to open a position";
			if (IsZoneLocked(side))
			{
				var gate = ZoneGate;
				return $"Locked by the zone gate -- {gate.Supporters(side.IsLong)} of {gate.ColumnCount} columns are {(side.IsLong ? "LOW/BELOW" : "HIGH/ABOVE")} (needs {gate.Threshold})";
			}

			return $"Place a market {(side.IsLong ? "buy" : "sell")} of {FormatVolume(OrderVolume)} units on {Symbol}";
		}

		private bool CanToggleBreakeven(QuickTradeSideState side) => HasSelection && (side.BreakevenArmed || side.HasOpenPositions);

		private string BreakevenDetail(QuickTradeSideState side) =>
			side.BreakevenArmed ? $"{SideName(side).ToLowerInvariant()} · waiting for 0+"
			: side.HasOpenPositions && _automation.AutoArm ? $"{SideName(side).ToLowerInvariant()} · auto after {_automation.ArmAfterSeconds}s"
			: side.HasOpenPositions ? $"{SideName(side).ToLowerInvariant()} · {side.OpenCount} open"
			: $"no {SideName(side).ToLowerInvariant()}";

		private string BreakevenTooltip(QuickTradeSideState side)
		{
			var name = SideName(side).ToLowerInvariant();
			if (!HasSelection)
				return "Open a chart to use close-at-breakeven";
			if (side.BreakevenArmed)
				return $"Armed: each {name} position on {Symbol} closes on its own once price is back to its entry or better (P&L 0 or positive). Turns off when no {name}s are left or a close fails. Click to disarm.";
			if (!side.HasOpenPositions)
				return $"No {name} positions on {Symbol} to close at breakeven";

			return $"Wait for each of the {side.OpenCount} {name} position(s) on {Symbol} to get back to its entry or better (P&L 0 or positive), then close it. Stays armed across reloads of this tab; closing the tab stops it.";
		}

		private static string FormatMoney(double? amount) => amount is { } value ? value.ToString("N2") : "--";

		private static string FormatVolume(double volume) =>
			volume.ToString("0.########", CultureInfo.InvariantCulture);
	}
}
