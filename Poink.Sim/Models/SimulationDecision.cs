// ── Brain observe/decide data shapes ─────────────────────────────────────
//  Split out of ISimulationBrain.cs so Models holds every plain data shape
//  the Brain/Trader/Simulation layers pass around, and Brain/ holds only the
//  ISimulationBrain contract itself.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using Poink.Sim.Repository;

namespace Poink.Sim.Models;

// Public (elevated from internal, like EncoderKind/ArchitectureKind/
// ActionOutputKind) because a brain's enabled subset of these travels on
// BrainRecipe (BrainRecipe.EnabledDirections) -- see ComposedPolicyBrain.Create.
// All 4 remain individually optional per brain, same as Volume/StopLoss/
// TakeProfit (see IActionOutputHead): a brain built with fewer than 4 gets a
// correspondingly narrower direction head instead of always-4 logits.
public enum SimulationAction { Hold, EnterLong, EnterShort, Close }

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
// EnterLong/EnterShort rather than new SimulationAction members because
// they're properties of *how* to enter, not a distinct direction choice.
// A ComposedPolicyBrain built with one or more IActionOutputHead (Volume/
// StopLoss/TakeProfit -- see BrainCatalog.ActionOutputKind) fills
// CapitalFraction/StopLossPrice/TakeProfitPrice from its network's own extra
// output heads instead of the fixed CapitalFraction=0.5/no-SL-TP fallback;
// that's the extension point for growing what a brain can decide, not new
// SimulationAction members -- these 4 direction choices are the fixed set to
// pick a subset from (see BrainRecipe.EnabledDirections), never grown or
// replaced. Reason: short text shown in the trade/decision log.
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
// Capital/Position are read-only Trader snapshots. RecentTrades is this
// trader's full closed-trade log, oldest-first (see Trader.TradeLog) --
// unlike RecentBars/RecentTicks it isn't ring-buffer-bounded, since a round-
// trip count naturally stays far smaller than a tick/bar count; fed for
// TraderStateEncoder's optional trade-history list features (win/loss and
// direction of the last N closed trades). Bid/Ask are this decision's
// exact current-tick quote -- CurrentPrice is already their mid (or a bar's
// Close, itself built from mid-price ticks), so neither aggregation mode lets
// a brain recover the spread from CurrentPrice alone; carried separately so
// SpreadVolatilityEncoder (or any future spread-aware brain) reads the real
// live spread regardless of aggregate/raw-tick mode.
internal readonly record struct SimulationObservation(
    DateTime TimestampUtc, double CurrentPrice,
    IReadOnlyList<HlocAvdtBar> RecentBars,
    IReadOnlyList<Tick> RecentTicks,
    IReadOnlyList<BarPriceHistogram> RecentBarHistograms,
    CapitalState Capital, Position? Position,
    IReadOnlyList<Trade> RecentTrades,
    double Bid, double Ask);
