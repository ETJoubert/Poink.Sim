using System;
using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Trader.Risk;

internal interface IRiskManager
{
	RiskReview Gate(SimulationDecision proposed, double bid, double ask, DateTime timestamp);

	IReadOnlyList<SimulationDecision> Supervise(double bid, double ask, DateTime timestamp);

	IReadOnlyList<RiskPolicyStatus> PolicyStatuses();

	IReadOnlyList<RiskPolicySnapshot> PolicyStates();

	bool PoliciesLocked { get; }

	bool SetPolicyEnabled(string policyName, bool enabled);

	bool MovePolicy(string policyName, int delta);

	bool SetPolicyParameter(string policyName, string key, double value);
}
