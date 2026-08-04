
using System;
using System.Collections.Generic;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Repository;
using Poink.Sim.Trader.Orders;

namespace Poink.Sim.Models;

internal sealed record SimulationConfig(
    string Table, DateTime From, DateTime To, bool Aggregate, TimeSpan BarSize,
    string BrainName, double StartingCash, int TraderCount,
    double Leverage = 1.0, double StopOutLevelPercent = 50.0, double CommissionRatePercent = 0.0);

internal sealed record SimulationLogEntry(
    DateTime TimestampUtc, string Action, double Price, PositionDirection Direction,
    double Size, double? RealizedPnl, double Commission, string? Reason);


internal enum TraderControlMode { Auto, Manual }


internal sealed record SimulationProgress(
    int CursorIndex, int TotalCount, DateTime? CurrentTimestampUtc, DateTime RangeFromUtc, DateTime RangeToUtc);


internal sealed record TraderSnapshot(
    int Id, string Label, TraderControlMode Mode, CapitalState Capital, Position? Position,
    PendingOrder? PendingOrder, int TradeCount,
    IReadOnlyList<SimulationLogEntry> RecentLog, IReadOnlyList<Trade> Trades,
    double MaxEquity, double MaxDrawdown, double Leverage, double StopOutLevelPercent,
    IReadOnlyList<RlInputFeature>? Inputs, IReadOnlyList<RlOutputFeature>? Outputs);

internal sealed record SimulationSnapshot(
    SimulationConfig Config, bool IsPlaying, bool IsFinished, double SpeedMultiplier, bool IsUncapped,
    int CursorIndex, int TotalCount, DateTime? CurrentTimestampUtc, double? CurrentPrice,
    double? CurrentBid, double? CurrentAsk,
    IReadOnlyList<TraderSnapshot> Traders,
    IReadOnlyList<HlocAvdtBar> RecentBars, IReadOnlyList<BarPriceHistogram> RecentBarHistograms,
    IReadOnlyList<Tick> RecentTicks);
