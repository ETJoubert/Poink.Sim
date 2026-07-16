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

Poink.Sim was split out of Poink.ML because simulation playback isn't Poink.ML's
training pipeline — it's a separate app with its own lifecycle (play/pause/step/
speed against live Capital/Position). It does carry its own TorchSharp/Microsoft.ML
(for a future trained `ISimulationBrain`, see "Not yet implemented" below) plus
Postgres tick access and the shared Blazor terminal UI (`Poink.Shared.Client`), but
never a `ProjectReference` to Poink.ML itself: `ITickRepository`/`TickRepository`
and the `HlocAvdtBar` bar shape are each duplicated here rather than shared, so
this project's build/deploy stays independent of Poink.ML's.

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

`SetMaxSpeed()` ("Max"/"Turbo" on the page) is a separate **uncapped** mode, not just
a bigger multiplier: `TimerCallback` drains every remaining tick in one callback
instead of a bounded slice, so a run finishes in a single publish no matter how many
ticks are left. Max and Turbo run the engine identically — the only difference is a
page-side rendering choice (Turbo also flips `_hideChart` on), since the engine has
no concept of "hidden."

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
- **Playback** — Play/Pause, Step (paused-only), speed buttons (1000x/10000x, plus
  uncapped Max/Turbo — see below), Reset (rewind, same config), New Simulation (back
  to setup), Hide/Show Chart (manually skip chart rendering for faster trading at
  high speed). Live: a candlestick chart (aggregation on) or a scrolling raw-tick
  table (aggregation off), a Capital/Position readout (cash, realized/unrealized
  P&L, equity, position), and a trade/decision log.
- **Max vs. Turbo** — both run the backend at uncapped speed (drain every remaining
  tick in one go, see "Playback design" above); Max keeps the chart/tick table
  rendering, Turbo hides them automatically (same effect as the Hide Chart toggle)
  so only the Capital/Position stats and the trade/decision log update — useful for
  very large runs where redrawing the chart is the actual bottleneck, not the
  simulation math itself.

## Files

Organized by role rather than one flat feature folder:

| folder | file | role |
|---|---|---|
| `Models/` | `HlocAvdtBar.cs` | this project's own copy of Poink.ML's HLOC/AVDT bar shape |
| `Models/` | `SimulationAccount.cs` | `Position`/`CapitalState`/`Trade` domain shapes (simple cash P&L) |
| `Models/` | `SimulationDecision.cs` | `SimulationAction`/`SimulationDecision`/`SimulationObservation` — the Brain's observe/decide data shapes |
| `Models/` | `SimulationSnapshot.cs` | `SimulationConfig`/`SimulationSnapshot`/`SimulationLogEntry` read-model |
| `Brain/` | `ISimulationBrain.cs` | brain contract |
| `Brain/` | `MomentumThresholdBrain.cs` | heuristic placeholder brain (percent-change threshold) |
| `Trader/` | `Trader.cs` | applies decisions to Capital/Position, computes P&L |
| `Simulation/` | `SimulationBarAggregator.cs` | incremental (stateful) tick → HLOC/AVDT bar aggregator |
| `Simulation/` | `SimulationEngine.cs` | orchestrating singleton: playback timer + MarketSim + Aggregator + Brain + Trader |
| `Repository/` | `ITickRepository.cs` / `TickRepository.cs` | Postgres tick data access |
| `Components/Pages/` | `SimulationPage.razor(.cs/.css)` | the `/` page (setup form + live playback view) |
| `Components/Charts/` | `OhlcvChart.razor(.cs/.css)` | candlestick chart component |
| `Components/Layout/` | `MainLayout.razor`, `SimNavLinks.razor` | app shell + nav |

## Build log

- [x] Design: domain model, playback timer strategy, aggregation toggle semantics
- [x] Domain shapes (`SimulationAccount.cs`), incremental aggregator, `Trader`
- [x] `ISimulationBrain` + `MomentumThresholdBrain` heuristic placeholder
- [x] `SimulationEngine` (materialize-once playback, play/pause/step/speed, single-flight load)
- [x] `SimulationPage` (setup form, live chart/tick table, capital/position panel, trade log)
- [x] DI + navigation wiring (`Program.cs`, `SimNavLinks.razor`)
- [x] Hide/show chart toggle for faster high-speed playback
- [x] Split out of Poink.ML into its own project (Poink.Sim) — not analytics
- [x] Reorganized from one flat feature folder into Models/Brain/Trader/Simulation/Repository/Components
- [ ] End-to-end verification against real tick data (build + run + walk through the page)
- [ ] Trained ML brain, stop-loss/take-profit, spread/commission cost (see "Not yet implemented")
