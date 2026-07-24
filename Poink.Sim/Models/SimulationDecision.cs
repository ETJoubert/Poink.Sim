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

// Market: fill immediately (today's only behavior). Limit: fill at TriggerPrice
// or better (Buy Limit waits for price to fall to it, Sell Limit for it to
// rise). Stop: fill once price breaks through TriggerPrice (breakout entry).
// Deliberately not a new SimulationAction member -- see OrderType usage below.
internal enum OrderType { Market, Limit, Stop }

// CapitalFraction: 0..1 of current Cash to commit as margin on Enter*; ignored
// for Hold/Close. StopLossPrice/TakeProfitPrice: auto-close trigger levels for
// the resulting position, acted on every tick by Trader.Advance. OrderType/
// TriggerPrice: null/Market (the default) opens immediately as today; Limit/
// Stop with a TriggerPrice queues a pending order instead -- kept as fields on
// EnterLong/EnterShort rather than new SimulationAction members because the
// neural brains' output layer is hardwired to one logit per SimulationAction
// (see MlpArchitecture/LstmArchitecture's ActionCount=4), so growing the enum
// would break every already-trained network's weights. Reason: short text
// shown in the trade/decision log.
internal readonly record struct SimulationDecision(
    SimulationAction Action, double CapitalFraction = 0,
    double? StopLossPrice = null, double? TakeProfitPrice = null,
    OrderType? OrderType = null, double? TriggerPrice = null, string? Reason = null);

// What the Brain is allowed to see at one decision point: one raw tick
// (aggregation off) or one just-completed bar (aggregation on). RecentBars/
// RecentTicks/RecentBarHistograms are bounded, most-recent-last history
// windows built by SimulationEngine -- never anything beyond "now" (no
// lookahead). RecentBarHistograms is empty when aggregation is off (raw-tick
// mode has no bars, so no per-bar histograms either) -- a brain that reads it
// merges down to whatever window it needs itself (see BarPriceHistogram.Merge),
// the same way a brain slices its own trailing window out of RecentBars.
// Capital/Position are read-only Trader snapshots.
internal readonly record struct SimulationObservation(
    DateTime TimestampUtc, double CurrentPrice,
    IReadOnlyList<HlocAvdtBar> RecentBars,
    IReadOnlyList<Tick> RecentTicks,
    IReadOnlyList<BarPriceHistogram> RecentBarHistograms,
    CapitalState Capital, Position? Position);
