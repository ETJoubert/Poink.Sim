using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Models;
using Poink.Sim.Trader.Risk;

namespace Poink.Sim.Components.Shared.TradeConsole
{
	/// <summary>
	/// Order-entry panel shared by the live Trade page and the Simulation page -- order type, direction,
	/// volume, SL/TP and entry/projection tables. Carries no page-specific backend: submission and the
	/// optional chart-draft preview are handed back to the host page via callbacks, and it takes account
	/// context (balance, leverage, quotes) as plain parameters instead of injecting a session service.
	/// </summary>
	public partial class TradeConsole : ComponentBase
	{
		[Parameter]
		public double? CurrentBid { get; set; }

		[Parameter]
		public double? CurrentAsk { get; set; }

		/// <summary>Fallback fill price for callers with no live bid/ask spread (e.g. Simulation's raw-tick mode).</summary>
		[Parameter]
		public double? CurrentPrice { get; set; }

		[Parameter]
		public double PipSize { get; set; } = 0.0001;

		[Parameter]
		public double? Balance { get; set; }

		[Parameter]
		public double? Leverage { get; set; }

		/// <summary>The symbol's minimum SL/TP distance from entry, in price terms -- null skips the clamp.</summary>
		[Parameter]
		public double? MinStopLossDistancePrice { get; set; }

		[Parameter]
		public double? MinTakeProfitDistancePrice { get; set; }

		/// <summary>The symbol's volume step, in units -- null leaves a typed volume as entered.</summary>
		[Parameter]
		public double? SymbolVolumeStep { get; set; }

		[Parameter]
		public double? MinVolume { get; set; }

		[Parameter]
		public double? MaxVolume { get; set; }

		/// <summary>False when the symbol forbids short positions, which disables the SELL side.</summary>
		[Parameter]
		public bool AllowShort { get; set; } = true;

		/// <summary>The symbol's smallest price increment, used to round SL/TP prices -- null skips rounding.</summary>
		[Parameter]
		public double? PriceStep { get; set; }

		/// <summary>False when the host has nothing to submit against (e.g. Trade with no symbol selected).</summary>
		[Parameter]
		public bool HasSelection { get; set; } = true;

		/// <summary>Host-driven -- disables PLACE and shows "PLACING..." while a submission is in flight.</summary>
		[Parameter]
		public bool Busy { get; set; }

		/// <summary>Host-driven status line, shown after OnSubmit resolves.</summary>
		[Parameter]
		public string? StatusMessage { get; set; }

		[Parameter]
		public bool StatusIsError { get; set; }

		/// <summary>Shows the PREVIEW button and raises this only while a host has it wired up.</summary>
		[Parameter]
		public EventCallback<TradeConsoleDraft?> OnDraftChanged { get; set; }

		[Parameter]
		public EventCallback<TradeConsoleOrderRequest> OnSubmit { get; set; }

		/// <summary>
		/// Turns the console into a position-options editor: no order type or PLACE, edits are reported
		/// through <see cref="OnOptionsChanged"/> as price-independent <see cref="TradeConsoleOptions"/>.
		/// </summary>
		[Parameter]
		public bool SettingsMode { get; set; }

		/// <summary>Starting values, read once when the console is created.</summary>
		[Parameter]
		public TradeConsoleOptions? Options { get; set; }

		/// <summary>Raised whenever the edited values change, so a host can remember them across sessions.</summary>
		[Parameter]
		public EventCallback<TradeConsoleOptions> OnOptionsChanged { get; set; }

		private const double VolumeStep = 0.01;

		private OrderType _orderType = OrderType.Market;
		private bool _isLong = true;
		private double _triggerPrice;
		private VolumeSizingMode _volumeSizingMode = VolumeSizingMode.Manual;
		private double? _volumeUnits = 0.01;
		private double? _riskPercent;
		private SizingMode _stopLossMode = SizingMode.Pips;
		private double? _stopLossPips;
		private double? _stopLossPercent;
		private SizingMode _takeProfitMode = SizingMode.Pips;
		private double? _takeProfitPips;
		private double? _takeProfitPercent;
		private string? _localError;
		private bool _draftActive;

		private TradeConsoleOptions? _lastEmittedOptions;

		private string? DisplayStatus => _localError ?? StatusMessage;

		private bool DisplayStatusIsError => _localError is not null || StatusIsError;

		private PositionDirection Direction => _isLong ? PositionDirection.Long : PositionDirection.Short;

		private double? MarketFillPrice => (_isLong ? CurrentAsk : CurrentBid) ?? CurrentPrice;

		private double? EntryReferencePrice =>
			_orderType == OrderType.Market ? MarketFillPrice
			: _triggerPrice > 0 ? _triggerPrice
			: null;

		private double Volume =>
			WithinSymbolVolumeLimits(_volumeSizingMode == VolumeSizingMode.Risk ? CalculatedVolume ?? 0 : _volumeUnits ?? 0);

		private double EffectiveVolumeStep => SymbolVolumeStep is > 0 ? SymbolVolumeStep.Value : VolumeStep;

		private double WithinSymbolVolumeLimits(double volume)
		{
			if (volume <= 0)
				return volume;
			if (SymbolVolumeStep is > 0)
				volume = RiskHelper.RoundToStep(volume, SymbolVolumeStep.Value);
			if (MinVolume is { } min)
				volume = Math.Max(volume, min);
			return MaxVolume is > 0 ? Math.Min(volume, MaxVolume.Value) : volume;
		}

		/// <summary>
		/// Position size solved from the stop-loss pip distance and the percent of balance risked,
		/// rounded to the nearest <see cref="VolumeStep"/>. Null when any input is missing.
		/// </summary>
		private double? CalculatedVolume
		{
			get
			{
				if (EntryReferencePrice is not { } entry || entry <= 0)
					return null;
				if (_stopLossPips is not { } pips || pips <= 0)
					return null;
				if (_riskPercent is not { } percent || percent <= 0)
					return null;
				if (Balance is not { } balance || balance <= 0)
					return null;

				double stopPrice = RiskHelper.StopLossPriceFromPips(Direction, entry, pips, PipSize);
				double riskAmount = percent / 100.0 * balance;
				return RiskHelper.SizeForRiskRounded(riskAmount, entry, stopPrice, EffectiveVolumeStep);
			}
		}

		private bool HasStopLoss =>
			_stopLossMode == SizingMode.Pips ? _stopLossPips is not null : _stopLossPercent is not null;

		private double? StopLossPrice
		{
			get
			{
				if (EntryReferencePrice is not { } entry || entry <= 0)
					return null;
				double? price = _stopLossMode == SizingMode.Pips
					? _stopLossPips is { } pips
						? RiskHelper.StopLossPriceFromPips(Direction, entry, pips, PipSize)
						: null
					: _stopLossPercent is { } percent
						? RiskHelper.StopLossPriceForSizeRisk(Direction, Volume, entry, percent / 100.0 * (Balance ?? 0)) is
							{ } raw
							? RiskHelper.ClampToMinimumDistance(entry, raw, MinStopLossDistancePrice ?? 0)
							: null
						: null;
				return price is { } p ? RiskHelper.RoundToStep(p, PriceStep ?? 0) : null;
			}
		}

		private double? TakeProfitPrice
		{
			get
			{
				if (EntryReferencePrice is not { } entry || entry <= 0)
					return null;
				double? price = _takeProfitMode == SizingMode.Pips
					? _takeProfitPips is { } pips
						? RiskHelper.TakeProfitPriceFromPips(Direction, entry, pips, PipSize)
						: null
					: _takeProfitPercent is { } percent
						? RiskHelper.TakeProfitPriceForSizeReward(Direction, Volume, entry, percent / 100.0 * (Balance ?? 0)) is
							{ } raw
							? RiskHelper.ClampToMinimumDistance(entry, raw, MinTakeProfitDistancePrice ?? 0)
							: null
						: null;
				return price is { } p ? RiskHelper.RoundToStep(p, PriceStep ?? 0) : null;
			}
		}

		private double? ProjectedLoss =>
			StopLossPrice is { } stop && EntryReferencePrice is { } entry
				? RiskHelper.RiskOnStop(Direction, Volume, entry, stop)
				: null;

		private double? ProjectedProfit =>
			TakeProfitPrice is { } target && EntryReferencePrice is { } entry
				? RiskHelper.RewardOnTarget(Direction, Volume, entry, target)
				: null;

		private double? ProjectedLossPercent =>
			ProjectedLoss is { } loss && Balance is > 0 ? RiskHelper.RiskFraction(loss, Balance.Value) : null;

		private double? ProjectedProfitPercent =>
			ProjectedProfit is { } profit && Balance is > 0 ? RiskHelper.RiskFraction(profit, Balance.Value) : null;

		private double? RewardToRisk =>
			ProjectedLoss is { } risk && ProjectedProfit is { } reward ? RiskHelper.RewardToRiskRatio(risk, reward) : null;

		private double? MarginUsed =>
			EntryReferencePrice is { } entry && Leverage is { } leverage
				? RiskHelper.MarginRequired(Volume, entry, leverage)
				: null;

		private double? MarginFree => MarginUsed is { } used && Balance is { } balance ? balance - used : null;

		private double? MarginUsedPercent =>
			MarginUsed is { } used && Balance is > 0 ? RiskHelper.RiskFraction(used, Balance.Value) : null;

		private double? MarginFreePercent =>
			MarginFree is { } free && Balance is > 0 ? RiskHelper.RiskFraction(free, Balance.Value) : null;

		private double? EntryDistancePips =>
			EntryReferencePrice is { } entry && MarketFillPrice is { } market
				? RiskHelper.PriceDistanceInPips(entry, market, PipSize)
				: null;

		private double? StopLossDistancePips =>
			StopLossPrice is { } stop && EntryReferencePrice is { } entry
				? RiskHelper.PriceDistanceInPips(entry, stop, PipSize)
				: null;

		private double? TakeProfitDistancePips =>
			TakeProfitPrice is { } target && EntryReferencePrice is { } entry
				? RiskHelper.PriceDistanceInPips(target, entry, PipSize)
				: null;

		private static string FormatPips(double? pips) => pips is { } value ? $"{value:N1}" : "--";

		private string LossRowClass => ProjectedLoss is { } loss ? (loss > 0 ? "loss" : "gain") : "loss";

		private string LossRowTitle =>
			ProjectedLoss is not null
				? "Money lost if the stop loss is hit, for this volume from the entry price"
				: HasStopLoss
					? "An entry price is needed to project the stop loss -- enter a trigger price, or wait for a market quote"
					: "No stop loss set -- this position has unlimited downside";

		private string LossValueText =>
			ProjectedLoss is { } loss ? $"{(loss > 0 ? "-" : "+")}{Math.Abs(loss):N2}"
			: HasStopLoss ? "--"
			: "no stop set";

		private string LossPercentText => ProjectedLossPercent is { } pct ? $"{Math.Abs(pct):P1}" : "--";

		private string ProfitValueText => ProjectedProfit is { } profit ? $"+{Math.Abs(profit):N2}" : "--";

		private string ProfitPercentText => ProjectedProfitPercent is { } pct ? $"{Math.Abs(pct):P1}" : "--";

		private string RewardToRiskText => RewardToRisk is { } ratio ? $"{ratio:N2} : 1" : "--";

		private string MarginUsedText => MarginUsed is { } used ? $"{used:N2}" : "--";

		private string MarginUsedPercentText => MarginUsedPercent is { } pct ? $"{pct:P1}" : "--";

		private string MarginFreeText => MarginFree is { } free ? $"{free:N2}" : "--";

		private string MarginFreePercentText => MarginFreePercent is { } pct ? $"{pct:P1}" : "--";

		protected override void OnInitialized()
		{
			if (Options is { } options)
				ApplyOptions(options);
			_lastEmittedOptions = CurrentOptions();
		}

		protected override void OnAfterRender(bool firstRender)
		{
			EmitOptionsIfChanged();

			if (!OnDraftChanged.HasDelegate || !_draftActive)
				return;

			if (EntryReferencePrice is { } entry)
				_ = OnDraftChanged.InvokeAsync(new TradeConsoleDraft(_isLong, entry, Volume, StopLossPrice, TakeProfitPrice));
			else
				_ = OnDraftChanged.InvokeAsync(null);
		}

		private void ToggleDraft()
		{
			_draftActive = !_draftActive;
			if (!_draftActive)
				_ = OnDraftChanged.InvokeAsync(null);
		}

		/// <summary>Called by the host (via @ref) once its own submission succeeds, to end the live preview.</summary>
		public void ClearDraftPreview()
		{
			if (!_draftActive)
				return;
			_draftActive = false;
			_ = OnDraftChanged.InvokeAsync(null);
		}

		private async Task SubmitEntry(bool isLong)
		{
			_isLong = isLong;
			_localError = null;

			if (!isLong && !AllowShort)
			{
				_localError = "Short selling is not allowed for this symbol.";
				return;
			}

			if (!HasSelection)
			{
				_localError = "No symbol selected.";
				return;
			}
			if (Volume <= 0)
			{
				_localError =
					_volumeSizingMode == VolumeSizingMode.Risk
						? "Enter a stop loss and a percent of balance to risk so the volume can be solved."
						: "Enter a volume greater than zero.";
				return;
			}
			if (_orderType != OrderType.Market && _triggerPrice <= 0)
			{
				_localError = "Enter a trigger price for a Limit/Stop order.";
				return;
			}

			await OnSubmit.InvokeAsync(
				new TradeConsoleOrderRequest(isLong, Volume, _orderType, _orderType == OrderType.Market ? null : _triggerPrice, StopLossPrice, TakeProfitPrice)
			);
		}

		private TradeConsoleOptions CurrentOptions() =>
			new(
				_isLong,
				_volumeSizingMode,
				_volumeUnits,
				_riskPercent,
				_stopLossMode,
				_stopLossPips,
				_stopLossPercent,
				_takeProfitMode,
				_takeProfitPips,
				_takeProfitPercent
			);

		private void EmitOptionsIfChanged()
		{
			if (!OnOptionsChanged.HasDelegate)
				return;
			var current = CurrentOptions();
			if (current == _lastEmittedOptions)
				return;
			_lastEmittedOptions = current;
			_ = OnOptionsChanged.InvokeAsync(current);
		}

		private void ApplyOptions(TradeConsoleOptions options)
		{
			_isLong = options.IsLong;
			_volumeSizingMode = options.VolumeMode;
			_volumeUnits = options.VolumeUnits;
			_riskPercent = options.RiskPercent;
			_stopLossMode = options.StopLossMode;
			_stopLossPips = options.StopLossPips;
			_stopLossPercent = options.StopLossPercent;
			_takeProfitMode = options.TakeProfitMode;
			_takeProfitPips = options.TakeProfitPips;
			_takeProfitPercent = options.TakeProfitPercent;
		}

		private void SwitchToRiskSizing()
		{
			_volumeSizingMode = VolumeSizingMode.Risk;
			_stopLossMode = SizingMode.Pips;
		}

		private void ResetDraft()
		{
			_localError = null;
			_orderType = OrderType.Market;
			_triggerPrice = 0;
			_volumeSizingMode = VolumeSizingMode.Manual;
			_volumeUnits = 0.01;
			_riskPercent = null;
			_stopLossMode = SizingMode.Pips;
			_stopLossPips = null;
			_stopLossPercent = null;
			_takeProfitMode = SizingMode.Pips;
			_takeProfitPips = null;
			_takeProfitPercent = null;
		}
	}
}
