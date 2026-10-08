using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
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
			if (!firstRender)
				return;

			_storedVolume = await LocalStorage.GetItemAsync<double?>(VolumeStorageKey);
			StateHasChanged();
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

		private static string BreakevenDetail(QuickTradeSideState side) =>
			side.BreakevenArmed ? $"{SideName(side).ToLowerInvariant()} · waiting for 0+"
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
