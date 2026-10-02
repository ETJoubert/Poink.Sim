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

	/// <summary>Each trader's per-trade equity record by trader id; kept current at max speed too, when no snapshot is published.</summary>
	IReadOnlyDictionary<int, IReadOnlyList<TradeEquityPoint>> TradeEquityByTrader { get; }

	event Action? OnChanged;
	event Action? OnProgress;
	event Action? OnTradeEquityChanged;

	/// <summary>Every trader's account figures at the latest quote; kept current at max speed too, when no snapshot is published.</summary>
	SimulationFinancialsState? Financials { get; }

	event Action? OnFinancialsChanged;

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
	bool ApplyAccountOptions(BotAccountOptions options);
	void SetRobotOptions(RobotOptions options);
	bool SubmitManualDecision(int traderId, SimulationDecision decision, out RiskPolicyDenial? denial);
}
