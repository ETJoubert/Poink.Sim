
using System;
using System.Collections.Generic;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Repository;
using Poink.Sim.Trader;
using Poink.Sim.Trader.Orders;
using Poink.Sim.Trader.Risk;

namespace Poink.Sim.Models;

// Cell steps a decision on a fixed-width price band instead of the bar clock -- only
// meaningful while Aggregate is true.
internal enum SimulationSteppingMode { Bar, Cell }

internal sealed record SimulationConfig(
    string Table, DateTime From, DateTime To, bool Aggregate, TimeSpan BarSize,
    string BrainName, double StartingCash, int TraderCount,
    double Leverage = 1.0, double StopOutLevelPercent = 50.0, double CommissionRatePercent = 0.0,
    double PipSize = 0.01, IReadOnlyList<string>? RiskPolicyNames = null, bool LockRiskPolicies = false,
    SimulationSteppingMode SteppingMode = SimulationSteppingMode.Bar, double CellWidth = 0.10);

// Live PriceCellTracker readout, published only while SteppingMode is Cell -- lets a host
// compare how often it fires against the bar-based alternative.
internal sealed record SimulationCellState(long CellIndex, double CellLow, double CellHigh, int StepCount, DateTime LastStepUtc);

internal sealed record SimulationLogEntry(
    DateTime TimestampUtc, string Action, double Price, PositionDirection Direction,
    double Size, double? RealizedPnl, double Commission, string? Reason);


internal sealed record SimulationProgress(
    int CursorIndex, int TotalCount, DateTime? CurrentTimestampUtc, DateTime RangeFromUtc, DateTime RangeToUtc);


internal sealed record TraderSnapshot(
    int Id, string Label, TraderControlMode Mode, CapitalState Capital, Position? Position,
    PendingOrder? PendingOrder, int TradeCount,
    IReadOnlyList<SimulationLogEntry> RecentLog, IReadOnlyList<Trade> Trades,
    double MaxEquity, double MaxDrawdown, double Leverage, double StopOutLevelPercent,
    IReadOnlyList<RlInputFeature>? Inputs, IReadOnlyList<RlOutputFeature>? Outputs,
    IReadOnlyList<RiskPolicyStatus> RiskPolicyStatuses, RiskPolicyDenial? LastDenial);

internal sealed record SimulationSnapshot(
    SimulationConfig Config, bool IsPlaying, bool IsFinished, double SpeedMultiplier, bool IsUncapped,
    int CursorIndex, int TotalCount, DateTime? CurrentTimestampUtc, double? CurrentPrice,
    double? CurrentBid, double? CurrentAsk,
    IReadOnlyList<TraderSnapshot> Traders,
    IReadOnlyList<HlocAvdtBar> RecentBars, IReadOnlyList<BarPriceHistogram> RecentBarHistograms,
    IReadOnlyList<Tick> RecentTicks,
    IReadOnlyList<RiskPolicySnapshot> RiskPolicies, bool RiskPoliciesLocked,
    SimulationCellState? CellState);
