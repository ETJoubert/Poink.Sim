using System;
using System.Collections.Generic;
using Poink.Sim.Brain;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models;
using Poink.Sim.Trader.Orders;

namespace Poink.Sim.Trader;

/// <summary>
/// Executes trading decisions against one account. Advances stop loss, take profit,
/// margin stop-out and pending orders on every tick, and charges commission on open and close.
/// </summary>
internal sealed class Trader : ITrader
{
	private readonly List<Trade> _trades = new();
	private readonly TraderSettings _settings;
	private int _nextTradeId = 1;
	private double _peakEquity;
	private PendingOrder? _pendingOrder;
	private double _openCommissionCharge;

	private double? _lastMidPrice;

	public Trader(double startingCash, TraderSettings? settings = null)
	{
		Cash = startingCash;
		_peakEquity = startingCash;
		MaxEquity = startingCash;
		_settings = settings ?? new TraderSettings();
		Brain = _settings.Brain;
	}

	public double Cash { get; private set; }
	public double RealizedPnl { get; private set; }
	public Position? CurrentPosition { get; private set; }
	public PendingOrder? CurrentPendingOrder => _pendingOrder;
	public IReadOnlyList<Trade> TradeLog => _trades;

	public double Leverage => _settings.Leverage;
	public double StopOutLevelPercent => _settings.StopOutLevelPercent;

	public double MarginUsed => CurrentPosition is { } p ? p.Size * p.EntryPrice / _settings.Leverage : 0;

	public double MaxEquity { get; private set; }
	public double MaxDrawdown { get; private set; }

	public ISimulationBrain? Brain { get; set; }
	public TraderControlMode ControlMode { get; set; } = TraderControlMode.Auto;
	public bool CaptureIo { get; set; }
	public IReadOnlyList<RlInputFeature>? LatestInputs { get; set; }
	public IReadOnlyList<RlOutputFeature>? LatestOutputs { get; set; }

	/// <summary>
	/// Computes the current capital snapshot. Idempotent, not a pure read: MaxEquity/MaxDrawdown
	/// are high-water marks updated here, but a redundant call with unchanged bid/ask re-derives
	/// the same state and the mark updates become no-ops, which is what makes calling this from
	/// Observe (after Advance already ran this tick) safe.
	/// </summary>
	public CapitalState Snapshot(double bid, double ask)
	{
		double unrealized = CurrentPosition?.UnrealizedPnl(bid, ask) ?? 0;
		double marginUsed = MarginUsed;
		double equity = Cash + unrealized;
		double? marginLevel = marginUsed > 0 ? equity / marginUsed * 100.0 : null;

		var state = new CapitalState(Cash, RealizedPnl, unrealized, marginUsed, marginLevel);
		if (state.Equity > _peakEquity)
			_peakEquity = state.Equity;
		if (_peakEquity > MaxEquity)
			MaxEquity = _peakEquity;
		double drawdown = _peakEquity - state.Equity;
		if (drawdown > MaxDrawdown)
			MaxDrawdown = drawdown;
		return state;
	}

	public MaintenanceResult Advance(double bid, double ask, DateTime timestamp)
	{
		double mid = (bid + ask) / 2.0;
		if (CurrentPosition is { } openPosition)
		{
			double distance = _lastMidPrice is { } lastMid ? Math.Abs(mid - lastMid) : 0;
			CurrentPosition = openPosition with
			{
				TicksHeld = openPosition.TicksHeld + 1,
				CumulativeDistance = openPosition.CumulativeDistance + distance,
				PeakUnrealizedPnl = Math.Max(openPosition.PeakUnrealizedPnl, openPosition.UnrealizedPnl(bid, ask)),
			};
		}
		_lastMidPrice = mid;

		List<TradeExecution>? fills = null;

		if (CurrentPosition is { } position)
		{
			double exitPrice = position.Direction == PositionDirection.Long ? bid : ask;
			TradeExecution? autoClose =
				position.StopLossPrice is { } sl && StopLossBreached(position.Direction, exitPrice, sl)
					? Close(bid, ask, timestamp, $"stop-loss hit @ {exitPrice:F5}", TradeCloseReason.StopLoss)
				: position.TakeProfitPrice is { } tp && TakeProfitBreached(position.Direction, exitPrice, tp)
					? Close(bid, ask, timestamp, $"take-profit hit @ {exitPrice:F5}", TradeCloseReason.TakeProfit)
				: null;
			if (autoClose is { } fill)
				(fills ??= new List<TradeExecution>()).Add(fill);
		}

		if (CurrentPosition is { } stillOpen)
		{
			double marginUsed = MarginUsed;
			double marginLevel =
				marginUsed > 0
					? (Cash + stillOpen.UnrealizedPnl(bid, ask)) / marginUsed * 100.0
					: double.PositiveInfinity;
			if (marginLevel < _settings.StopOutLevelPercent)
			{
				var stopOut = Close(
					bid,
					ask,
					timestamp,
					$"stop-out: margin level {marginLevel:F1}% < {_settings.StopOutLevelPercent:F1}%",
					TradeCloseReason.StopOut
				);
				if (stopOut is { } fill)
					(fills ??= new List<TradeExecution>()).Add(fill);
			}
		}

		if (CurrentPosition is null && _pendingOrder is { } pending && IsTriggered(pending, bid, ask))
		{
			_pendingOrder = null;
			var triggered = Open(
				pending.Direction,
				pending.CapitalFraction,
				pending.StopLossPrice,
				pending.TakeProfitPrice,
				bid,
				ask,
				timestamp,
				pending.Reason
			);
			if (triggered is { } fill)
				(fills ??= new List<TradeExecution>()).Add(fill);
		}

		return new MaintenanceResult(
			Snapshot(bid, ask),
			(IReadOnlyList<TradeExecution>?)fills ?? Array.Empty<TradeExecution>()
		);
	}

	public TradeExecution? Apply(SimulationDecision decision, double bid, double ask, DateTime timestamp)
	{
		return decision.Action switch
		{
			SimulationAction.Hold => null,
			SimulationAction.Close => CurrentPosition is not null
				? Close(bid, ask, timestamp, decision.Reason, TradeCloseReason.Manual)
				: CancelPendingOrder(),
			SimulationAction.EnterLong => EnterOrQueue(PositionDirection.Long, decision, bid, ask, timestamp),
			SimulationAction.EnterShort => EnterOrQueue(PositionDirection.Short, decision, bid, ask, timestamp),
			_ => null,
		};
	}

	private TradeExecution? CancelPendingOrder()
	{
		_pendingOrder = null;
		return null;
	}

	private TradeExecution? EnterOrQueue(
		PositionDirection direction,
		SimulationDecision decision,
		double bid,
		double ask,
		DateTime time
	)
	{
		if (CurrentPosition is not null || _pendingOrder is not null)
			return null;

		var orderType = decision.OrderType ?? OrderType.Market;
		if (orderType == OrderType.Market)
			return Open(
				direction,
				decision.CapitalFraction,
				decision.StopLossPrice,
				decision.TakeProfitPrice,
				bid,
				ask,
				time,
				decision.Reason
			);

		if (decision.TriggerPrice is not { } triggerPrice)
			return null;

		_pendingOrder = new PendingOrder(
			direction,
			orderType,
			triggerPrice,
			decision.CapitalFraction,
			decision.StopLossPrice,
			decision.TakeProfitPrice,
			time,
			decision.Reason
		);
		// queued, not filled -- no decision-log entry until it triggers
		return null;
	}

	private TradeExecution? Open(
		PositionDirection direction,
		double capitalFraction,
		double? stopLossPrice,
		double? takeProfitPrice,
		double bid,
		double ask,
		DateTime time,
		string? reason
	)
	{
		double fillPrice = direction == PositionDirection.Long ? ask : bid;
		if (fillPrice <= 0)
			return null;

		double fraction = Math.Clamp(capitalFraction, 0, 1);
		double committed = fraction * Cash;
		if (committed <= 0)
			return null;

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

	private TradeExecution? Close(double bid, double ask, DateTime time, string? reason, TradeCloseReason closeReason)
	{
		if (CurrentPosition is not { } position)
			return null;

		double exitPrice = position.Direction == PositionDirection.Long ? bid : ask;
		double grossPnl = position.UnrealizedPnl(bid, ask);
		double closeCommission = _settings.EffectiveCommissionModel.CommissionFor(position.Size, exitPrice);
		double totalCommission = _openCommissionCharge + closeCommission;
		// open leg's commission was already charged at Open
		double netPnl = grossPnl - closeCommission;

		Cash += netPnl;
		RealizedPnl += netPnl;
		_trades.Add(
			new Trade(
				_nextTradeId++,
				position.Direction,
				position.Size,
				position.EntryPrice,
				position.EntryTime,
				exitPrice,
				time,
				grossPnl - totalCommission,
				totalCommission,
				reason,
				closeReason
			)
		);
		CurrentPosition = null;
		_openCommissionCharge = 0;
		return new TradeExecution(
			SimulationAction.Close,
			position.Direction,
			position.Size,
			grossPnl - totalCommission,
			totalCommission,
			reason
		);
	}

	private static bool StopLossBreached(PositionDirection direction, double exitPrice, double stopLossPrice) =>
		direction == PositionDirection.Long ? exitPrice <= stopLossPrice : exitPrice >= stopLossPrice;

	private static bool TakeProfitBreached(PositionDirection direction, double exitPrice, double takeProfitPrice) =>
		direction == PositionDirection.Long ? exitPrice >= takeProfitPrice : exitPrice <= takeProfitPrice;

	private static bool IsTriggered(PendingOrder order, double bid, double ask) =>
		(order.Direction, order.OrderType) switch
		{
			(PositionDirection.Long, OrderType.Limit) => ask <= order.TriggerPrice,
			(PositionDirection.Long, OrderType.Stop) => ask >= order.TriggerPrice,
			(PositionDirection.Short, OrderType.Limit) => bid >= order.TriggerPrice,
			(PositionDirection.Short, OrderType.Stop) => bid <= order.TriggerPrice,
			// OrderType.Market never rests as a pending order
			_ => false,
		};
}
