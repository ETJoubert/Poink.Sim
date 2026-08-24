using System;
using System.Collections.Generic;
using Poink.Sim.Brain;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models;
using Poink.Sim.Trader.Orders;

namespace Poink.Sim.Trader.Risk;

internal sealed class RiskAwareTrader : ITrader, IRiskInspectable
{
	private readonly ITrader _core;
	private readonly IRiskManager _riskManager;

	public RiskAwareTrader(ITrader core, IRiskManager riskManager)
	{
		_core = core;
		_riskManager = riskManager;
	}

	public IReadOnlyList<RiskPolicyStatus> PolicyStatuses() => _riskManager.PolicyStatuses();

	public IReadOnlyList<RiskPolicySnapshot> PolicyStates() => _riskManager.PolicyStates();

	public bool PoliciesLocked => _riskManager.PoliciesLocked;

	public bool SetPolicyEnabled(string policyName, bool enabled) => _riskManager.SetPolicyEnabled(policyName, enabled);

	public bool MovePolicy(string policyName, int delta) => _riskManager.MovePolicy(policyName, delta);

	public bool SetPolicyParameter(string policyName, string key, double value) =>
		_riskManager.SetPolicyParameter(policyName, key, value);

	public RiskPolicyDenial? LastDenial { get; private set; }

	public double Cash => _core.Cash;
	public double RealizedPnl => _core.RealizedPnl;
	public Position? CurrentPosition => _core.CurrentPosition;
	public PendingOrder? CurrentPendingOrder => _core.CurrentPendingOrder;
	public IReadOnlyList<Trade> TradeLog => _core.TradeLog;
	public double Leverage => _core.Leverage;
	public double StopOutLevelPercent => _core.StopOutLevelPercent;
	public double MarginUsed => _core.MarginUsed;
	public double MaxEquity => _core.MaxEquity;
	public double MaxDrawdown => _core.MaxDrawdown;

	public ISimulationBrain? Brain
	{
		get => _core.Brain;
		set => _core.Brain = value;
	}
	public TraderControlMode ControlMode
	{
		get => _core.ControlMode;
		set => _core.ControlMode = value;
	}
	public bool CaptureIo
	{
		get => _core.CaptureIo;
		set => _core.CaptureIo = value;
	}
	public IReadOnlyList<RlInputFeature>? LatestInputs
	{
		get => _core.LatestInputs;
		set => _core.LatestInputs = value;
	}
	public IReadOnlyList<RlOutputFeature>? LatestOutputs
	{
		get => _core.LatestOutputs;
		set => _core.LatestOutputs = value;
	}

	public CapitalState Snapshot(double bid, double ask) => _core.Snapshot(bid, ask);

	// Core maintenance runs first (stop-loss/take-profit/stop-out), then policy-injected
	// supervisory actions execute directly without re-entering the gate, being the policy's own.
	public MaintenanceResult Advance(double bid, double ask, DateTime timestamp)
	{
		var maintenance = _core.Advance(bid, ask, timestamp);
		var injected = _riskManager.Supervise(bid, ask, timestamp);
		if (injected.Count == 0)
			return maintenance;

		var fills = new List<TradeExecution>(maintenance.AutomaticFills);
		foreach (var decision in injected)
		{
			var execution = _core.Apply(decision, bid, ask, timestamp);
			if (execution is { } fill)
				fills.Add(fill);
		}

		return new MaintenanceResult(_core.Snapshot(bid, ask), fills);
	}

	public TradeExecution? Apply(SimulationDecision decision, double bid, double ask, DateTime timestamp)
	{
		var review = _riskManager.Gate(decision, bid, ask, timestamp);
		if (review.Verdict == RiskVerdict.Deny)
		{
			LastDenial = new RiskPolicyDenial(review.PolicyName ?? "risk policy", review.Reason ?? "denied", timestamp);
			return new TradeExecution(
				decision.Action,
				DirectionFor(decision.Action),
				0,
				null,
				0,
				review.Reason,
				Blocked: true,
				DeniedByPolicy: review.PolicyName
			);
		}

		LastDenial = null;
		return _core.Apply(review.Decision, bid, ask, timestamp);
	}

	private static PositionDirection DirectionFor(SimulationAction action) =>
		action == SimulationAction.EnterShort ? PositionDirection.Short : PositionDirection.Long;
}
