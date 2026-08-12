using Microsoft.AspNetCore.Components;
using Poink.Sim.Models;
using Poink.Sim.Simulation;
using Poink.Sim.Trader.Risk;

namespace Poink.Sim.Components.Simulation
{

    public partial class TraderControlsPanel : ComponentBase
    {
        [Inject] private SimulationEngine Service { get; set; } = default!;

        [Parameter] public int? TraderId { get; set; }
        [Parameter] public string TraderLabel { get; set; } = "";
        [Parameter] public bool IsManual { get; set; }

        [Parameter] public string? PositionSummary { get; set; }
        [Parameter] public string? PendingSummary { get; set; }

        [Parameter] public double Cash { get; set; }
        [Parameter] public double Leverage { get; set; } = 1.0;
        [Parameter] public double StopOutLevelPercent { get; set; } = 50.0;
        [Parameter] public double PipSize { get; set; } = 0.01;
        [Parameter] public double? CurrentPrice { get; set; }
        [Parameter] public double? CurrentBid { get; set; }
        [Parameter] public double? CurrentAsk { get; set; }


        private OrderType _orderType = OrderType.Market;
        private bool _isLong = true;
        private double _triggerPrice;
        private double _capitalFraction = 0.5;
        private double? _stopLossPips;
        private double? _takeProfitPips;
        private string? _error;

        private PositionDirection Direction => _isLong ? PositionDirection.Long : PositionDirection.Short;

        private double? MarketFillPrice =>
            (_isLong ? CurrentAsk : CurrentBid) ?? CurrentPrice;

        private double? EntryReferencePrice =>
            _orderType == OrderType.Market ? MarketFillPrice : _triggerPrice > 0 ? _triggerPrice : null;

        private double? StopLossPrice =>
            _stopLossPips is { } pips && EntryReferencePrice is { } entry && entry > 0
                ? RiskHelper.StopLossPriceFromPips(Direction, entry, pips, PipSize)
                : null;

        private double? TakeProfitPrice =>
            _takeProfitPips is { } pips && EntryReferencePrice is { } entry && entry > 0
                ? RiskHelper.TakeProfitPriceFromPips(Direction, entry, pips, PipSize)
                : null;

        private void ToggleManualMode()
        {
            if (TraderId is not { } id)
                return;
            Service.SetTraderMode(id, IsManual ? TraderControlMode.Auto : TraderControlMode.Manual);
        }

        private void SubmitEntry(bool isLong)
        {
            _error = null;
            if (TraderId is not { } id)
            { _error = "no trader selected"; return; }
            if (!IsManual)
            { _error = "switch this trader to Manual first"; return; }
            if (_capitalFraction is <= 0 or > 1)
            { _error = "capital fraction must be between 0 and 1"; return; }
            if (_orderType != OrderType.Market && _triggerPrice <= 0)
            { _error = "enter a trigger price for a Limit/Stop order"; return; }

            var action = isLong ? SimulationAction.EnterLong : SimulationAction.EnterShort;
            var orderType = _orderType == OrderType.Market ? (OrderType?)null : _orderType;
            var triggerPrice = _orderType == OrderType.Market ? (double?)null : _triggerPrice;
            var decision = new SimulationDecision(
                action, _capitalFraction, StopLossPrice, TakeProfitPrice, orderType, triggerPrice, Reason: "manual");

            if (!Service.SubmitManualDecision(id, decision))
                _error = "order rejected -- already in a position or an order is already resting";
        }

        private void SubmitCloseOrCancel()
        {
            _error = null;
            if (TraderId is not { } id)
            { _error = "no trader selected"; return; }
            if (!IsManual)
            { _error = "switch this trader to Manual first"; return; }

            var decision = new SimulationDecision(SimulationAction.Close, Reason: "manual");
            Service.SubmitManualDecision(id, decision);
        }
    }
}
