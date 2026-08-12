
using System;
using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Simulation;

internal interface ISimulationEngine : IDisposable
{
    IReadOnlyList<string> BrainNames { get; }
    SimulationSnapshot? Snapshot { get; }
    bool IsLoading { get; }
    string? Error { get; }
    SimulationProgress? Progress { get; }

    event Action? OnChanged;
    event Action? OnProgress;

    bool TryLoad(SimulationConfig config);
    bool TryLoadDummy(SimulationConfig config, int tickCount = 20_000);
    bool Play();
    bool Pause();
    bool Step();
    void SetSpeed(double multiplier);
    void SetMaxSpeed();
    bool SetAggregation(bool aggregate, TimeSpan barSize);
    bool Reset();
    void Unload();
    bool SetTraderMode(int traderId, TraderControlMode mode);
    bool SetRiskPolicyEnabled(string policyName, bool enabled);
    void SelectTraderForIo(int? traderId);
    bool SubmitManualDecision(int traderId, SimulationDecision decision);
}
