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
		private ITradeExecutionAccess Session { get; set; } = null!;

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

		private OrderType _orderType = OrderType.Market;
		private bool _isLong = true;
		private double _triggerPrice;
		private double? _volumeUnits;
		private double? _stopLossPips;
		private double? _takeProfitPips;
		private string? _status;
		private bool _statusIsError;
		private bool _busy;

		private PositionDirection Direction => _isLong ? PositionDirection.Long : PositionDirection.Short;

		private double? MarketFillPrice => _isLong ? CurrentAsk : CurrentBid;

		private double? EntryReferencePrice =>
			_orderType == OrderType.Market ? MarketFillPrice
			: _triggerPrice > 0 ? _triggerPrice
			: null;

		private double? StopLossPrice =>
			EntryReferencePrice is { } entry && _stopLossPips is { } pips
				? RiskHelper.StopLossPriceFromPips(Direction, entry, pips, PipSize)
				: null;

		private double? TakeProfitPrice =>
			EntryReferencePrice is { } entry && _takeProfitPips is { } pips
				? RiskHelper.TakeProfitPriceFromPips(Direction, entry, pips, PipSize)
				: null;

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
				Session.UseLiveEnvironment
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
				var result = await Session.PlaceOrderAsync(
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

		private async Task SubmitCloseOrCancel()
		{
			_status = null;

			if (SymbolId is not { } symbolId)
			{
				SetStatus("No symbol selected.", isError: true);
				return;
			}

			_busy = true;
			StateHasChanged();
			try
			{
				var result = await Session.CloseSymbolAsync(symbolId);
				SetStatus(result.Message, !result.Success);
			}
			finally
			{
				_busy = false;
			}
		}

		private void SetStatus(string message, bool isError)
		{
			_status = message;
			_statusIsError = isError;
		}
	}
}
