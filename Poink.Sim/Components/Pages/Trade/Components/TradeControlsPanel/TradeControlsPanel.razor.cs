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

		private OrderType _orderType = OrderType.Market;
		private bool _isLong = true;
		private double _triggerPrice;
		private double? _volumeUnits = 0.01;
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

		private double Volume => _volumeUnits ?? 0;

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
			if (_volumeUnits is not (> 0))
			{
				SetStatus("Enter a volume greater than zero.", isError: true);
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
					_volumeUnits.Value,
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

		private void ResetDraft()
		{
			_status = null;
			_orderType = OrderType.Market;
			_triggerPrice = 0;
			_volumeUnits = 0.01;
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
