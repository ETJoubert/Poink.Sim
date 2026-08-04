using Microsoft.AspNetCore.Components;
using Poink.Sim.Models;
using Poink.Sim.Simulation;

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


        private OrderType _orderType = OrderType.Market;
        private double _triggerPrice;
        private double _capitalFraction = 0.5;
        private double? _stopLoss;
        private double? _takeProfit;
        private string? _error;

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
                action, _capitalFraction, _stopLoss, _takeProfit, orderType, triggerPrice, Reason: "manual");

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
