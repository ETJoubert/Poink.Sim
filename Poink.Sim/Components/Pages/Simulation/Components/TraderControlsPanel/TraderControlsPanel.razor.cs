using Microsoft.AspNetCore.Components;
using Poink.Sim.Models;
using Poink.Sim.Simulation;

namespace Poink.Sim.Components.Simulation
{
    // Manual trader controls -- acts on the trader identified by TraderId
    // (SimulationPage passes its SelectedTrader's fields). Toggle it to Manual
    // first (ToggleManualMode), then this ticket submits straight to
    // SimulationEngine.SubmitManualDecision, bypassing the Brain entirely for
    // that session. Parameters are plain primitives rather than the internal
    // TraderSnapshot record -- this component (like every other one in this
    // project) has to stay `public partial class` for the Razor-generated half
    // to compile, and TraderSnapshot's own field types (Position/PendingOrder/
    // TraderControlMode/...) are internal, so exposing it directly here would
    // force those internal domain types public too. Injects SimulationEngine
    // directly (the same DI singleton SimulationPage holds) rather than routing
    // actions back up through parent EventCallbacks.
    public partial class TraderControlsPanel : ComponentBase
    {
        [Inject] private SimulationEngine Service { get; set; } = default!;

        [Parameter] public int? TraderId { get; set; }
        [Parameter] public string TraderLabel { get; set; } = "";
        [Parameter] public bool IsManual { get; set; }

        // Pre-formatted by SimulationPage (which can freely read the internal
        // Position/PendingOrder types) -- null means flat / no resting order.
        [Parameter] public string? PositionSummary { get; set; }
        [Parameter] public string? PendingSummary { get; set; }

        // Reused across Buy/Sell so a trader can flip direction without
        // re-entering size/SL/TP/order-type.
        private OrderType _orderType = OrderType.Market;
        private double _triggerPrice;
        private double _capitalFraction = 0.5;
        private double? _stopLoss;
        private double? _takeProfit;
        private string? _error;

        private void ToggleManualMode()
        {
            if (TraderId is not { } id) return;
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

        // Trader.Apply's Close action already does double duty (closes the open
        // position, else cancels a resting pending order, else no-op) -- see
        // Trader.cs -- so one button covers both without this panel needing to
        // know which applies.
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
