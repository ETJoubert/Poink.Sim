# Simulation — interactive trader replay

> Status: **in progress** — heuristic placeholder brain only (`MomentumThresholdBrain`);
> no trained ML brain, stop-loss/take-profit, or spread/commission cost yet.

## What this feature is

A live, interactive replay of historical ticks driven by three pieces:

- **MarketSim** — streams historical ticks for a chosen symbol and date range from
  Postgres (`ITickRepository`), materialized once per run and played back under
  user control (play/pause/step/speed), either raw or aggregated into HLOC/AVDT bars.
- **Brain** (`ISimulationBrain`) — observes the stream (raw ticks or completed bars,
  whichever the aggregation toggle selects) plus the current Capital/Position, and
  decides whether to hold, enter long/short, or close.
- **Trader** — applies the Brain's decisions, tracking Capital and one open Position
  at a time, with simple cash P&L (no leverage/margin: capital moves 1:1 with
  price × size). This is a CFD-style account, so shorting is a first-class case.

## Why this is its own project

Poink.Sim was split out of Poink.ML because simulation playback isn't analytics —
it doesn't train anything. It carries no TorchSharp/Microsoft.ML dependency, only
Postgres tick access and the shared Blazor terminal UI (`Poink.Shared.Client`).
`ITickRepository`/`TickRepository` and the `HlocAvdtBar` bar shape are each
duplicated here rather than referenced from Poink.ML, specifically to avoid
pulling that dependency weight back in.

## Playback design

`ITickRepository.ReadTicks` stays untouched (a lazy Postgres iterator); `SimulationEngine`
materializes a run's ticks once into an in-memory list with an integer cursor, because a
pausable/steppable/speed-controlled sim needs random access and can't hold a live DB
reader across a pause.

Playback runs on one `System.Threading.Timer` per engine, used as a **self-rescheduling
one-shot** (period = `Infinite`; the callback re-arms itself at the end) so callbacks
never overlap and Pause/SetSpeed take effect cleanly at a callback boundary. The speed
multiplier controls **how many ticks advance per callback**, not how fast the timer
fires (fixed ~100ms cadence) — this bounds UI update traffic to ~10/sec regardless of
speed, so high multipliers play through history faster without flooding the page.

Mark-to-market (`Trader.Snapshot`) runs every raw tick so Capital/Position stay live
between decisions; `Brain.Decide` + `Trader.Apply` run once per raw tick when
aggregation is off, or once per completed bar when aggregation is on — this is what
makes the aggregation toggle actually control what the Brain sees.

`SimulationBarAggregator` is a stateful class exposing "feed one tick, get zero-or-one
completed bar back," since a sim advances tick-by-tick as the user plays/steps forward
(unlike Poink.ML's batch, static, whole-list-up-front aggregators). The per-tick math
mirrors Poink.ML's `HlocAvdtAggregator.Aggregate` exactly. `HlocAvdtBar.cs` is this
project's own copy of that bar shape (see "Why this is its own project" above).

Like every other `*TrainingService` singleton in Poink.ML, `SimulationEngine` holds
one shared in-memory "current run" for the whole process, not per-user/session — this
is intentional, matching that convention, not a gap.

## Not yet implemented

- **Trained ML brain.** `ISimulationBrain` is deliberately not shaped like
  `IForecastModel`/`IQuantileBrain`/`IPatternBrain` (no `Build`/`BuildInput`, no
  TorchSharp `Module`) — a heuristic brain has no weights. A future trained brain can
  still implement the same interface, holding a loaded `Module<Tensor,Tensor>`
  privately and doing its own tensor prep inside `Decide`.
- **Stop-loss / take-profit.** `Position` and `SimulationDecision` already carry
  `StopLossPrice`/`TakeProfitPrice` fields; `Trader.Apply` has a marked-but-unimplemented
  hook (`// SL/TP CHECK`) where auto-close logic slots in later without reworking
  the shape.
- **Spread / commission cost.** Trades execute at mid price today; `Tick.Bid`/`Ask`
  are already available for wiring this in later.
- **Multiple concurrent positions / pyramiding.** v1 allows only one open position
  at a time; reversing direction requires an explicit `Close` first.

## Using it

`dotnet run --project Poink.Sim`, browse to `http://localhost:5001` — the Simulation
page is the app's landing page (`/`, aliased at `/simulation`).

- **Setup** — pick a symbol (from the tick tables Postgres has), a date range, the
  aggregation toggle (raw ticks vs. HLOC/AVDT bars) and bar size, a brain, and
  starting cash, then **Load Simulation**.
- **Playback** — Play/Pause, Step (paused-only), speed buttons (1x/5x/20x/100x),
  Reset (rewind, same config), New Simulation (back to setup), Hide/Show Chart
  (skip chart rendering for faster trading at high speed). Live: a candlestick
  chart (aggregation on) or a scrolling raw-tick table (aggregation off), a
  Capital/Position readout (cash, realized/unrealized P&L, equity, position), and a
  trade/decision log.

## Files

| file | role |
|---|---|
| `ISimulationBrain.cs` | brain contract + `SimulationObservation`/`SimulationAction`/`SimulationDecision` |
| `MomentumThresholdBrain.cs` | heuristic placeholder brain (percent-change threshold) |
| `SimulationAccount.cs` | `Position`/`CapitalState`/`Trade` domain shapes (simple cash P&L) |
| `Trader.cs` | applies decisions to Capital/Position, computes P&L |
| `HlocAvdtBar.cs` | this project's own copy of Poink.ML's HLOC/AVDT bar shape |
| `SimulationBarAggregator.cs` | incremental (stateful) tick → HLOC/AVDT bar aggregator |
| `SimulationSnapshot.cs` | `SimulationConfig`/`SimulationSnapshot`/`SimulationLogEntry` read-model |
| `SimulationEngine.cs` | orchestrating singleton: playback timer + MarketSim + Aggregator + Brain + Trader |
| `SimulationPage.razor(.cs/.css)` | the `/` page (setup form + live playback view) |

## Build log

- [x] Design: domain model, playback timer strategy, aggregation toggle semantics
- [x] Domain shapes (`SimulationAccount.cs`), incremental aggregator, `Trader`
- [x] `ISimulationBrain` + `MomentumThresholdBrain` heuristic placeholder
- [x] `SimulationEngine` (materialize-once playback, play/pause/step/speed, single-flight load)
- [x] `SimulationPage` (setup form, live chart/tick table, capital/position panel, trade log)
- [x] DI + navigation wiring (`Program.cs`, `SimNavLinks.razor`)
- [x] Hide/show chart toggle for faster high-speed playback
- [x] Split out of Poink.ML into its own project (Poink.Sim) — not analytics
- [ ] End-to-end verification against real tick data (build + run + walk through the page)
- [ ] Trained ML brain, stop-loss/take-profit, spread/commission cost (see "Not yet implemented")
