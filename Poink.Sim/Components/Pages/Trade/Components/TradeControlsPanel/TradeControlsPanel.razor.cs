using Microsoft.AspNetCore.Components;
using Poink.Sim.Models;
using Poink.Sim.Trader.Risk;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>
	/// Order-entry preview for the live Trade page -- lays out the same fields as Simulation's
	/// TraderControlsPanel (order type, direction, volume, SL/TP) but stays purely local state.
	/// PLACE/CLOSE report a status message instead of submitting, since there is no live order
	/// service to wire this to yet.
	/// </summary>
	public partial class TradeControlsPanel : ComponentBase
	{
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

		private void SubmitEntry(bool isLong)
		{
			_isLong = isLong;
			_status = "Preview only -- order placement isn't wired up to cTrader yet.";
		}

		private void SubmitCloseOrCancel() => _status = "Preview only -- order placement isn't wired up to cTrader yet.";
	}
}
