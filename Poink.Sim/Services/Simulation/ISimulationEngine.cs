using System;
using System.Collections.Generic;
using Poink.Sim.Models;
using Poink.Sim.Trader;
using Poink.Sim.Trader.Risk;

namespace Poink.Sim.Simulation;

internal interface ISimulationEngine : IDisposable
{
	SimulationSnapshot? Snapshot { get; }
	bool IsLoading { get; }
	string? Error { get; }
	SimulationProgress? Progress { get; }

	event Action? OnChanged;
	event Action? OnProgress;

	bool TryLoad(SimulationConfig config);
	bool Play();
	bool Pause();
	bool Step();
	bool Seek(int targetIndex);
	void SetSpeed(double multiplier);
	void SetMaxSpeed();
	bool SetAggregation(bool aggregate, TimeSpan barSize);
	bool Reset();
	void Unload();
	bool SetRiskPolicyEnabled(string policyName, bool enabled);
	bool MoveRiskPolicy(string policyName, int delta);
	bool SetRiskPolicyParameter(string policyName, string key, double value);
	void SetRobotOptions(RobotOptions options);
	bool SubmitManualDecision(int traderId, SimulationDecision decision, out RiskPolicyDenial? denial);
}
