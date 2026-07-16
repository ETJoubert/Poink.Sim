// ── Brain observe/decide data shapes ─────────────────────────────────────
//  Split out of ISimulationBrain.cs so Models holds every plain data shape
//  the Brain/Trader/Simulation layers pass around, and Brain/ holds only the
//  ISimulationBrain contract itself.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using Poink.Sim.Repository;

namespace Poink.Sim.Models;

internal enum SimulationAction { Hold, EnterLong, EnterShort, Close }

// CapitalFraction: 0..1 of current Cash to commit on Enter*; ignored for Hold/Close.
// StopLossPrice/TakeProfitPrice: reserved for the future stop-loss/take-profit
// extension (Trader does not act on them yet -- see Trader.cs). Reason: short
// text shown in the trade/decision log.
internal readonly record struct SimulationDecision(
    SimulationAction Action, double CapitalFraction = 0,
    double? StopLossPrice = null, double? TakeProfitPrice = null, string? Reason = null);

// What the Brain is allowed to see at one decision point: one raw tick
// (aggregation off) or one just-completed bar (aggregation on). RecentBars/
// RecentTicks are bounded, most-recent-last history windows built by
// SimulationEngine -- never anything beyond "now" (no lookahead). Capital/
// Position are read-only Trader snapshots.
internal readonly record struct SimulationObservation(
    DateTime TimestampUtc, double CurrentPrice,
    IReadOnlyList<HlocAvdtBar> RecentBars,
    IReadOnlyList<Tick> RecentTicks,
    CapitalState Capital, Position? Position);
