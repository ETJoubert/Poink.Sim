// ── Pluggable trading-decision contract ──────────────────────────────────
//  Deliberately NOT shaped like IForecastModel/IQuantileBrain/IPatternBrain
//  (no Build/BuildInput, no TorchSharp Module<Tensor,Tensor>) -- those exist
//  to construct/train a fresh model per run. A heuristic brain has no
//  weights and nothing to train. A future TRAINED brain can still implement
//  this exact interface: it would just hold a loaded Module<Tensor,Tensor>
//  as a private field and do its own tensor prep inside Decide -- this
//  interface only fixes the runtime observe/decide contract, not how (or
//  whether) a model is involved.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using Poink.Sim.Repositories;

namespace Poink.Sim.Features.Simulation;

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

internal interface ISimulationBrain
{
    string Name { get; }

    SimulationDecision Decide(SimulationObservation observation);

    // Called once per run start; stateless brains no-op.
    void Reset();
}
