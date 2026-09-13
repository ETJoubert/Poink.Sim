using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Poink.Sim.Models;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Trader.Risk;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>
	/// Order-entry panel for the live Trade page -- lays out the same fields as Simulation's
	/// TraderControlsPanel (order type, direction, volume, SL/TP) but submits through
	/// ICTraderSessionService instead of a simulated trader.
	/// </summary>
	public partial class TradeControlsPanel : ComponentBase
	{
		[Inject]
		private ITradeExecutionAccess TradeExecution { get; set; } = null!;

		[Inject]
		private ITickStreamAccess Session { get; set; } = null!;

		[Inject]
		private IJSRuntime JS { get; set; } = null!;

		[Parameter]
		public long? SymbolId { get; set; }

		[Parameter]
		public string? SymbolName { get; set; }

		[Parameter]
		public double? CurrentBid { get; set; }

		[Parameter]
		public double? CurrentAsk { get; set; }

		[Parameter]
		public double PipSize { get; set; } = 0.0001;

		private enum SizingMode
		{
			Pips,
			PercentBalance,
		}

		private enum VolumeSizingMode
		{
			Manual,
			Risk,
		}

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
		private string? _status;
		private bool _statusIsError;
		private bool _busy;

		private PositionDirection Direction => _isLong ? PositionDirection.Long : PositionDirection.Short;

		private double? MarketFillPrice => _isLong ? CurrentAsk : CurrentBid;

		private double? EntryReferencePrice =>
			_orderType == OrderType.Market ? MarketFillPrice
			: _triggerPrice > 0 ? _triggerPrice
			: null;

		private double Volume =>
			_volumeSizingMode == VolumeSizingMode.Risk ? CalculatedVolume ?? 0 : _volumeUnits ?? 0;

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
				if (Session.Balance is not { } balance || balance <= 0)
					return null;

				double stopPrice = RiskHelper.StopLossPriceFromPips(Direction, entry, pips, PipSize);
				double riskAmount = percent / 100.0 * balance;
				double rawSize = RiskHelper.SizeForRisk(riskAmount, entry, stopPrice);
				if (rawSize <= 0)
					return null;

				double rounded = Math.Round(Math.Round(rawSize / VolumeStep) * VolumeStep, 2);
				return rounded > 0 ? rounded : null;
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
				return _stopLossMode == SizingMode.Pips
					? _stopLossPips is { } pips
						? RiskHelper.StopLossPriceFromPips(Direction, entry, pips, PipSize)
						: null
					: _stopLossPercent is { } percent
						? RiskHelper.StopLossPriceForSizeRisk(Direction, Volume, entry, percent / 100.0 * (Session.Balance ?? 0))
						: null;
			}
		}

		private double? TakeProfitPrice
		{
			get
			{
				if (EntryReferencePrice is not { } entry || entry <= 0)
					return null;
				return _takeProfitMode == SizingMode.Pips
					? _takeProfitPips is { } pips
						? RiskHelper.TakeProfitPriceFromPips(Direction, entry, pips, PipSize)
						: null
					: _takeProfitPercent is { } percent
						? RiskHelper.TakeProfitPriceForSizeReward(Direction, Volume, entry, percent / 100.0 * (Session.Balance ?? 0))
						: null;
			}
		}

		private double? ProjectedLoss =>
			StopLossPrice is { } stop && EntryReferencePrice is { } entry
				? RiskHelper.RiskOnStop(Direction, Volume, entry, stop)
				: null;

		private double? ProjectedProfit =>
			TakeProfitPrice is { } target && EntryReferencePrice is { } entry
				? -RiskHelper.RiskOnStop(Direction, Volume, entry, target)
				: null;

		private double? ProjectedLossPercent =>
			ProjectedLoss is { } loss && Session.Balance is > 0 ? loss / Session.Balance.Value : null;

		private double? ProjectedProfitPercent =>
			ProjectedProfit is { } profit && Session.Balance is > 0 ? profit / Session.Balance.Value : null;

		private double? RewardToRisk =>
			ProjectedLoss is { } risk && risk > 0 && ProjectedProfit is { } reward && reward > 0 ? reward / risk : null;

		private async Task SubmitEntry(bool isLong)
		{
			_isLong = isLong;
			_status = null;

			if (SymbolId is not { } symbolId)
			{
				SetStatus("No symbol selected.", isError: true);
				return;
			}
			if (Volume <= 0)
			{
				SetStatus(
					_volumeSizingMode == VolumeSizingMode.Risk
						? "Enter a stop loss and a percent of balance to risk so the volume can be solved."
						: "Enter a volume greater than zero.",
					isError: true
				);
				return;
			}
			if (_orderType != OrderType.Market && _triggerPrice <= 0)
			{
				SetStatus("Enter a trigger price for a Limit/Stop order.", isError: true);
				return;
			}

			if (
				TradeExecution.UseLiveEnvironment
				&& !await JS.InvokeAsync<bool>(
					"confirm",
					$"Place a LIVE {(isLong ? "BUY" : "SELL")} order for {SymbolName ?? "this symbol"}?"
				)
			)
				return;

			_busy = true;
			StateHasChanged();
			try
			{
				var result = await TradeExecution.PlaceOrderAsync(
					symbolId,
					isLong,
					Volume,
					_orderType switch
					{
						OrderType.Limit => CTraderOrderType.Limit,
						OrderType.Stop => CTraderOrderType.Stop,
						_ => CTraderOrderType.Market,
					},
					_orderType == OrderType.Market ? null : _triggerPrice,
					StopLossPrice,
					TakeProfitPrice
				);
				SetStatus(result.Message, !result.Success);
			}
			finally
			{
				_busy = false;
			}
		}

		private void SwitchToRiskSizing()
		{
			_volumeSizingMode = VolumeSizingMode.Risk;
			_stopLossMode = SizingMode.Pips;
		}

		private void ResetDraft()
		{
			_status = null;
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

		private void SetStatus(string message, bool isError)
		{
			_status = message;
			_statusIsError = isError;
		}
	}
}
