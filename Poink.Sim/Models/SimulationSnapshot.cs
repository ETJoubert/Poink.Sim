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
    string BrainName, double StartingCash, int TraderCount);

internal sealed record SimulationLogEntry(
    DateTime TimestampUtc, string Action, double Price, string? Detail);

// Cheap sibling to SimulationSnapshot below -- just enough to place the
// current tick on a From/To timeline, so a component can track live progress
// (including through an uncapped Max run's rapid slices, see SimulationEngine.
// PublishProgress) without paying for a full Snapshot's recent-ticks/bars/
// trader-log copies on every update.
internal sealed record SimulationProgress(
    int CursorIndex, int TotalCount, DateTime? CurrentTimestampUtc, DateTime RangeFromUtc, DateTime RangeToUtc);

// One trader's slice of the shared run -- all traders replay the same tick
// stream against the same shared Brain instance (see SimulationEngine), but
// each gets its own Capital/Position/log since Trader.Apply is per-instance.
internal sealed record TraderSnapshot(
    int Id, string Label, CapitalState Capital, Position? Position, int TradeCount,
    IReadOnlyList<SimulationLogEntry> RecentLog, IReadOnlyList<Trade> Trades,
    double MaxEquity, double MaxDrawdown);

// Immutable read-model published wholesale each callback -- the page reads
// the reference (no torn reads, no per-field locking needed on the page side).
internal sealed record SimulationSnapshot(
    SimulationConfig Config, bool IsPlaying, bool IsFinished, double SpeedMultiplier, bool IsUncapped,
    int CursorIndex, int TotalCount, DateTime? CurrentTimestampUtc, double? CurrentPrice,
    double? CurrentBid, double? CurrentAsk,
    IReadOnlyList<TraderSnapshot> Traders,
    IReadOnlyList<HlocAvdtBar> RecentBars, IReadOnlyList<Tick> RecentTicks);
