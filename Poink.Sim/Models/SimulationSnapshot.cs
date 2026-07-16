// ── Read-model published by SimulationEngine ─────────────────────────────
//  Live, rapidly-mutating playback state (up to ~10 replacements/sec while
//  playing) -- not the *MlResults.cs convention Poink.ML uses for completed,
//  in-memory training results. Kept entirely inside Poink.Sim; no cross-
//  project consumer needs it (the chart is fed via the shared CTraderBarRow
//  at the page boundary instead).
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using Poink.Sim.Repository;

namespace Poink.Sim.Models;

internal sealed record SimulationConfig(
    string Table, DateTime From, DateTime To, bool Aggregate, TimeSpan BarSize,
    string BrainName, double StartingCash);

internal sealed record SimulationLogEntry(
    DateTime TimestampUtc, string Action, double Price, string? Detail);

// Immutable read-model published wholesale each callback -- the page reads
// the reference (no torn reads, no per-field locking needed on the page side).
internal sealed record SimulationSnapshot(
    SimulationConfig Config, bool IsPlaying, bool IsFinished, double SpeedMultiplier,
    int CursorIndex, int TotalCount, DateTime? CurrentTimestampUtc, double? CurrentPrice,
    CapitalState Capital, Position? Position,
    IReadOnlyList<HlocAvdtBar> RecentBars, IReadOnlyList<Tick> RecentTicks,
    IReadOnlyList<SimulationLogEntry> RecentLog);
