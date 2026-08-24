using System;
using System.Collections.Generic;
using Poink.Sim.Brain;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models;
using Poink.Sim.Trader.Orders;

namespace Poink.Sim.Trader;

internal enum TraderControlMode
{
	Auto,
	Manual,
}

internal interface ITrader
{
	double Cash { get; }
	double RealizedPnl { get; }
	Position? CurrentPosition { get; }
	PendingOrder? CurrentPendingOrder { get; }
	IReadOnlyList<Trade> TradeLog { get; }

	double Leverage { get; }
	double StopOutLevelPercent { get; }
	double MarginUsed { get; }

	double MaxEquity { get; }
	double MaxDrawdown { get; }

	ISimulationBrain? Brain { get; set; }
	TraderControlMode ControlMode { get; set; }

	// True only for the one trader currently inspected in the UI -- see Observe.
	bool CaptureIo { get; set; }
	IReadOnlyList<RlInputFeature>? LatestInputs { get; set; }
	IReadOnlyList<RlOutputFeature>? LatestOutputs { get; set; }

	CapitalState Snapshot(double bid, double ask);
	MaintenanceResult Advance(double bid, double ask, DateTime timestamp);
	TradeExecution? Apply(SimulationDecision decision, double bid, double ask, DateTime timestamp);

	// Auto-mode traders decide for themselves from what they observe of the market; manual
	// traders no-op here since a human (or SubmitManualDecision) drives them directly.
	// CaptureIo gates the expensive DecideWithLogProbability/DescribeInputs path -- it builds an
	// autograd graph with no dispose scope, unlike the plain Decide() path, so only the one
	// trader the UI is currently inspecting should ever set it.
	TradeExecution? Observe(MarketWindow window)
	{
		if (ControlMode != TraderControlMode.Auto || Brain is null)
			return null;

		var observation = new SimulationObservation(
			window.TimestampUtc,
			window.CurrentPrice,
			window.RecentBars,
			window.RecentTicks,
			window.RecentBarHistograms,
			Snapshot(window.Bid, window.Ask),
			CurrentPosition,
			TradeLog,
			window.Bid,
			window.Ask
		);

		SimulationDecision decision;
		if (CaptureIo && Brain is IPolicyGradientBrain policyBrain)
		{
			var (decidedAction, _, _, outputs) = policyBrain.DecideWithLogProbability(observation);
			LatestInputs = policyBrain.DescribeInputs(observation);
			LatestOutputs = outputs;
			decision = decidedAction;
		}
		else
		{
			decision = Brain.Decide(observation);
		}

		return Apply(decision, window.Bid, window.Ask, window.TimestampUtc);
	}
}
