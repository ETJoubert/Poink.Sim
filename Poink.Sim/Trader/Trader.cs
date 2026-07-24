// ── Trade execution + P&L: leveraged CFD account ─────────────────────────
//  Applies Brain decisions to Capital/Position under real CFD mechanics:
//  leveraged margin sizing, spread-correct fills (buy at Ask, sell at Bid),
//  pluggable commission, and automatic SL/TP/margin-stop-out closes. Cash
//  only moves on realized P&L and commission -- opening a position locks
//  MarginUsed against Equity, it never withdraws from Cash (real broker
//  Balance semantics). v1 requires an explicit Close before reversing
//  direction; only one open position and at most one resting pending
//  order at a time.
//
//  Two entry points per tick: Advance (unconditional, every raw tick --
//  mark-to-market plus the SL/TP/stop-out/pending-order trigger checks,
//  which all need real intrabar precision, not just decision-point
//  precision) and Apply (once per Brain decision point -- market fills,
//  queuing a Limit/Stop pending order, or closing/cancelling).
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using Poink.Sim.Models;
using Poink.Sim.Trader.Orders;

namespace Poink.Sim.Trader;

internal sealed class Trader
{
    private readonly List<Trade> _trades = new();
    private readonly TraderSettings _settings;
    private int _nextTradeId = 1;
    private double _peakEquity;
    private PendingOrder? _pendingOrder;
    private double _openCommissionCharge;

    public Trader(double startingCash, TraderSettings? settings = null)
    {
        Cash = startingCash;
        _peakEquity = startingCash;
        MaxEquity = startingCash;
        _settings = settings ?? new TraderSettings();
    }

    public double Cash { get; private set; }
    public double RealizedPnl { get; private set; }
    public Position? CurrentPosition { get; private set; }
    public PendingOrder? CurrentPendingOrder => _pendingOrder;
    public IReadOnlyList<Trade> TradeLog => _trades;

    public double Leverage => _settings.Leverage;
    public double StopOutLevelPercent => _settings.StopOutLevelPercent;

    // Margin-at-entry convention: locked at Position.Size*EntryPrice/Leverage
    // when opened, held static for the life of the position (not re-priced as
    // the market moves), matching how real brokers report "used margin."
    public double MarginUsed => CurrentPosition is { } p ? p.Size * p.EntryPrice / _settings.Leverage : 0;

    // Tracked incrementally off every Snapshot() call, rather than from a
    // stored equity curve, so this stays O(1) memory for arbitrarily long runs.
    public double MaxEquity { get; private set; }
    public double MaxDrawdown { get; private set; }

    // Pure read -- no fills, safe to call redundantly (e.g. an out-of-band UI
    // refresh) alongside Advance's own internal call.
    public CapitalState Snapshot(double bid, double ask)
    {
        double unrealized = CurrentPosition?.UnrealizedPnl(bid, ask) ?? 0;
        double marginUsed = MarginUsed;
        double equity = Cash + unrealized;
        double? marginLevel = marginUsed > 0 ? equity / marginUsed * 100.0 : null;

        var state = new CapitalState(Cash, RealizedPnl, unrealized, marginUsed, marginLevel);
        if (state.Equity > _peakEquity) _peakEquity = state.Equity;
        if (_peakEquity > MaxEquity) MaxEquity = _peakEquity;
        double drawdown = _peakEquity - state.Equity;
        if (drawdown > MaxDrawdown) MaxDrawdown = drawdown;
        return state;
    }

    // Per-tick maintenance, unconditional (called every raw tick regardless of
    // the aggregation toggle -- see SimulationEngine/RlEpisodeRunner) so a
    // resting order or a breached SL/TP/stop-out fires the instant price
    // crosses it, not just at the next Brain decision point. Order: SL/TP
    // first (an intended exit; SL wins if both breach the same tick), then
    // stop-out (only relevant if still open), then a pending-order trigger
    // (only relevant once flat).
    public MaintenanceResult Advance(double bid, double ask, DateTime timestamp)
    {
        List<TradeExecution>? fills = null;

        if (CurrentPosition is { } position)
        {
            double exitPrice = position.Direction == PositionDirection.Long ? bid : ask;
            TradeExecution? autoClose =
                position.StopLossPrice is { } sl && StopLossBreached(position.Direction, exitPrice, sl)
                    ? Close(bid, ask, timestamp, $"stop-loss hit @ {exitPrice:F5}")
                    : position.TakeProfitPrice is { } tp && TakeProfitBreached(position.Direction, exitPrice, tp)
                        ? Close(bid, ask, timestamp, $"take-profit hit @ {exitPrice:F5}")
                        : null;
            if (autoClose is { } fill) (fills ??= new List<TradeExecution>()).Add(fill);
        }

        if (CurrentPosition is { } stillOpen)
        {
            double marginUsed = MarginUsed;
            double marginLevel = marginUsed > 0
                ? (Cash + stillOpen.UnrealizedPnl(bid, ask)) / marginUsed * 100.0
                : double.PositiveInfinity;
            if (marginLevel < _settings.StopOutLevelPercent)
            {
                var stopOut = Close(bid, ask, timestamp,
                    $"stop-out: margin level {marginLevel:F1}% < {_settings.StopOutLevelPercent:F1}%");
                if (stopOut is { } fill) (fills ??= new List<TradeExecution>()).Add(fill);
            }
        }

        if (CurrentPosition is null && _pendingOrder is { } pending && IsTriggered(pending, bid, ask))
        {
            _pendingOrder = null;
            var triggered = Open(pending.Direction, pending.CapitalFraction, pending.StopLossPrice,
                pending.TakeProfitPrice, bid, ask, timestamp, pending.Reason);
            if (triggered is { } fill) (fills ??= new List<TradeExecution>()).Add(fill);
        }

        return new MaintenanceResult(Snapshot(bid, ask), (IReadOnlyList<TradeExecution>?)fills ?? Array.Empty<TradeExecution>());
    }

    // Applies one Brain decision at the given tick's real Bid/Ask. Returns the
    // fill's structured details (for the decision log), or null for a no-op
    // (Hold, a queued pending order, or cancelling a pending order).
    public TradeExecution? Apply(SimulationDecision decision, double bid, double ask, DateTime timestamp)
    {
        return decision.Action switch
        {
            SimulationAction.Hold => null,
            SimulationAction.Close => CurrentPosition is not null
                ? Close(bid, ask, timestamp, decision.Reason)
                : CancelPendingOrder(),
            SimulationAction.EnterLong => EnterOrQueue(PositionDirection.Long, decision, bid, ask, timestamp),
            SimulationAction.EnterShort => EnterOrQueue(PositionDirection.Short, decision, bid, ask, timestamp),
            _ => null,
        };
    }

    // Close doing double duty as "cancel the resting pending order" (rather
    // than a dedicated CancelPending action) keeps SimulationAction at its
    // current 4 members -- see OrderType's doc comment on why that matters.
    private TradeExecution? CancelPendingOrder()
    {
        _pendingOrder = null;
        return null;
    }

    private TradeExecution? EnterOrQueue(PositionDirection direction, SimulationDecision decision, double bid, double ask, DateTime time)
    {
        if (CurrentPosition is not null || _pendingOrder is not null) return null;

        var orderType = decision.OrderType ?? OrderType.Market;
        if (orderType == OrderType.Market)
            return Open(direction, decision.CapitalFraction, decision.StopLossPrice, decision.TakeProfitPrice, bid, ask, time, decision.Reason);

        if (decision.TriggerPrice is not { } triggerPrice) return null; // Limit/Stop requires a trigger price

        _pendingOrder = new PendingOrder(direction, orderType, triggerPrice, decision.CapitalFraction,
            decision.StopLossPrice, decision.TakeProfitPrice, time, decision.Reason);
        return null; // queued, not filled -- no decision-log entry until it triggers
    }

    private TradeExecution? Open(PositionDirection direction, double capitalFraction, double? stopLossPrice,
        double? takeProfitPrice, double bid, double ask, DateTime time, string? reason)
    {
        double fillPrice = direction == PositionDirection.Long ? ask : bid;
        if (fillPrice <= 0) return null;

        // CapitalFraction is the fraction of Cash committed as margin, not
        // notional -- notional scales up by Leverage from there. At Leverage=1
        // this reproduces today's pre-CFD sizing exactly.
        double fraction = Math.Clamp(capitalFraction, 0, 1);
        double committed = fraction * Cash;
        if (committed <= 0) return null;

        double notional = committed * _settings.Leverage;
        double size = notional / fillPrice;
        double commission = _settings.EffectiveCommissionModel.CommissionFor(size, fillPrice);

        Cash -= commission;
        RealizedPnl -= commission;
        _openCommissionCharge = commission;

        CurrentPosition = new Position(direction, size, fillPrice, time, stopLossPrice, takeProfitPrice);
        var action = direction == PositionDirection.Long ? SimulationAction.EnterLong : SimulationAction.EnterShort;
        return new TradeExecution(action, direction, size, null, commission, reason);
    }

    private TradeExecution? Close(double bid, double ask, DateTime time, string? reason)
    {
        if (CurrentPosition is not { } position) return null;

        double exitPrice = position.Direction == PositionDirection.Long ? bid : ask;
        double grossPnl = position.UnrealizedPnl(bid, ask);
        double closeCommission = _settings.EffectiveCommissionModel.CommissionFor(position.Size, exitPrice);
        double totalCommission = _openCommissionCharge + closeCommission;
        double netPnl = grossPnl - closeCommission; // open leg's commission was already charged at Open

        Cash += netPnl;
        RealizedPnl += netPnl;
        _trades.Add(new Trade(_nextTradeId++, position.Direction, position.Size, position.EntryPrice,
            position.EntryTime, exitPrice, time, grossPnl - totalCommission, totalCommission, reason));
        CurrentPosition = null;
        _openCommissionCharge = 0;
        return new TradeExecution(SimulationAction.Close, position.Direction, position.Size, grossPnl - totalCommission, totalCommission, reason);
    }

    private static bool StopLossBreached(PositionDirection direction, double exitPrice, double stopLossPrice) =>
        direction == PositionDirection.Long ? exitPrice <= stopLossPrice : exitPrice >= stopLossPrice;

    private static bool TakeProfitBreached(PositionDirection direction, double exitPrice, double takeProfitPrice) =>
        direction == PositionDirection.Long ? exitPrice >= takeProfitPrice : exitPrice <= takeProfitPrice;

    // Standard FX/CFD trigger convention. Fills at the actual Bid/Ask (not the
    // trigger price itself), so a gap-through produces realistic slippage.
    private static bool IsTriggered(PendingOrder order, double bid, double ask) => (order.Direction, order.OrderType) switch
    {
        (PositionDirection.Long, OrderType.Limit) => ask <= order.TriggerPrice,
        (PositionDirection.Long, OrderType.Stop) => ask >= order.TriggerPrice,
        (PositionDirection.Short, OrderType.Limit) => bid >= order.TriggerPrice,
        (PositionDirection.Short, OrderType.Stop) => bid <= order.TriggerPrice,
        _ => false, // OrderType.Market never rests as a pending order
    };
}
