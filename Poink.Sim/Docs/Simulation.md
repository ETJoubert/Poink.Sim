# Simulation — interactive trader replay

> Doc version: **v1.0** — see "Changelog" at the bottom for what changed.
> See [README.md](README.md) for documentation standards.

## What it is

A live, interactive replay of historical ticks driven by three pieces:

- **Tick stream** — `ITickRepository.ReadTicks` streams historical ticks for
  a chosen symbol and date range straight from Postgres as a live,
  forward-only reader; `SimulationEngine` never materializes the selected
  range into memory.
- **Brain** (`ISimulationBrain`, see [../Brain/README.md](../Brain/README.md))
  — observes the stream (raw ticks or completed HLOC/AVDT bars, whichever the
  aggregation toggle selects) plus the current Capital/Position, and decides
  whether to hold, enter long/short, or close.
- **Trader** — applies the Brain's decisions under leveraged CFD mechanics:
  margin sizing, spread-correct fills (buy at Ask, sell at Bid), pluggable
  commission, and automatic SL/TP/margin-stop-out closes. Shorting is a
  first-class case, not a borrow-based short.

One run can drive **multiple trader sessions** in parallel (`TraderCount`),
each with its own `Trader` (Capital/Position) but sharing the one active
Brain instance. Any session can be flipped from Auto (Brain-driven) to
Manual per-session via `SimulationEngine.SetTraderMode` — a Manual session
ignores the Brain entirely and only moves via
`SimulationEngine.SubmitManualDecision`, driven by the Simulation page's
trader controls panel (order type, trigger price, capital fraction, SL/TP,
Buy/Sell/Close). `Trader.Advance` (mark-to-market, SL/TP, stop-out,
pending-order triggers) still runs unconditionally for every session
regardless of mode.

## Playback design

`SimulationEngine` plays a run back off a live, forward-only
`ITickRepository.ReadTicks` reader — playback only ever moves forward
(Play/Step/speed all advance the same reader), so a pausable/steppable/
speed-controlled sim never needs random access. `Reset` disposes the current
reader and opens a fresh one from the start of the configured range.
`TryLoadDummy`'s synthetic ticks are a small in-memory list instead (no
Postgres involved, via `DummyTickGenerator`), wrapped behind the same reader
abstraction so the playback/reset code doesn't need to know which source
it's replaying.

Playback runs on one `System.Threading.Timer` per engine, used as a
**self-rescheduling one-shot** (period = `Infinite`; the callback re-arms
itself at the end) so callbacks never overlap and Pause/SetSpeed take effect
cleanly at a callback boundary. The speed multiplier controls **how many
ticks advance per callback**, not how fast the timer fires (fixed ~100ms
cadence) — this bounds UI update traffic to ~10/sec regardless of speed, so
high multipliers play through history faster without flooding the page.

`SetMaxSpeed()` ("Max" on the page) is a separate **uncapped** mode, not
just a bigger multiplier: `TimerCallback` drains ticks for a fixed time
slice (`UncappedSliceMs`) instead of a bounded tick count, immediately
rescheduling for the next slice — as fast as the reader/brain allow overall,
but still publishing a lightweight progress-only snapshot roughly every
slice so the page's timeline visibly progresses instead of freezing until
one final publish (`PublishProgress` vs. the full `PublishSnapshot`). Hiding
the chart/tick table for faster rendering during a Max run is a separate,
independent page-side toggle (Hide Chart) — the engine has no concept of
"hidden."

Mark-to-market (`Trader.Advance`, covering SL/TP/stop-out/pending-order
triggers too) runs every raw tick for every session so Capital/Position stay
live between decisions; `Brain.Decide` + `Trader.Apply` run once per raw
tick when aggregation is off, or once per completed bar when aggregation is
on — this is what makes the aggregation toggle actually control what the
Brain sees. `SimulationBarAggregator` is a stateful class exposing "feed one
tick, get zero-or-one completed bar back," since a sim advances tick-by-tick
as the user plays/steps forward.

`SimulationEngine` holds one shared in-memory "current run" for the whole
process (a DI singleton, not per-user/session), matching the convention
every other orchestrating singleton in this project follows (see
[Training.md](Training.md)'s `RlTrainingService`).

## The CFD account (`Trader`)

`Trader` (implements `ITrader`, built via `ITraderFactory` for testability)
is a leveraged CFD account engine with two entry points per tick:

- **`Advance(bid, ask, timestamp)`** — unconditional per-tick maintenance:
  mark-to-market, then SL/TP checks (SL wins if both breach the same tick),
  then margin-call stop-out (`MarginLevel < StopOutLevelPercent`), then a
  resting pending-order trigger check. Runs every raw tick for every
  session regardless of the aggregation toggle or trade mode, since these
  need real intrabar precision.
- **`Apply(decision, bid, ask, timestamp)`** — one Brain (or manual)
  decision: a market fill, queuing a Limit/Stop `PendingOrder`, or
  closing/cancelling. `Close` does double duty — closes the open position if
  one exists, else cancels a resting pending order, else no-op.

Key mechanics: `CapitalFraction` sizes margin (not notional) as a fraction
of Cash; `notional = committed × Leverage`; fills always execute at the
correct side of the real spread (buy at Ask, sell at Bid — "exit-side
pricing" for marks too); commission is charged per fill leg via the
pluggable `ICommissionModel`; a Limit/Stop order queued via
`SimulationDecision.OrderType`/`TriggerPrice` rests until price crosses it,
filling at the actual Bid/Ask (so a gap-through produces realistic
slippage). Only one open position and at most one resting pending order per
session at a time — reversing direction requires an explicit `Close` first.

See [../GLOSSARY.md](../GLOSSARY.md) for the full term-by-term breakdown
(Margin Used/Free/Level, Equity, Drawdown, Exit-side pricing, etc.).

## Files

| Folder | File | Role |
|---|---|---|
| `Services/Simulation/` | `SimulationEngine.cs` | Orchestrating singleton: playback timer + tick reader + aggregator + Brain + one-or-more `Trader` sessions |
| `Services/Simulation/` | `SimulationBarAggregator.cs` | Incremental (stateful) tick → HLOC/AVDT bar aggregator |
| `Services/Simulation/` | `DummyTickGenerator.cs` | Synthetic random-walk ticks, shared with headless RL episodes (see [Training.md](Training.md)) |
| `Services/Simulation/` | `RingBuffer.cs` | Fixed-capacity circular buffer backing the engine's bounded recent-ticks/bars/log windows |
| `Trader/` | `Trader.cs`, `ITrader.cs` | The CFD account engine + its public contract |
| `Trader/` | `TraderFactory.cs`, `ITraderFactory.cs` | DI seam for `Trader` construction |
| `Trader/` | `TraderSettings.cs` | Leverage/stop-out/commission bundle passed to `Trader`'s constructor |
| `Trader/Commission/` | `ICommissionModel.cs`, `PercentOfNotionalCommission.cs` | Pluggable per-fill commission cost |
| `Trader/Orders/` | `PendingOrder.cs` | A resting Limit/Stop entry order |
| `Repository/` | `ITickRepository.cs`, `TickRepository.cs` | Postgres tick data access (streamed, chunk-pruned) |
| `Models/` | `HlocAvdtBar.cs`, `SimulationAccount.cs`, `SimulationDecision.cs`, `SimulationSnapshot.cs` | Domain/read-model shapes |
| `Components/Pages/Simulation/` | `SimulationPage.razor(.cs/.css)` | The `/` page (setup form + live playback view) |
| `Components/Pages/Simulation/Components/` | `TraderControlsPanel/` | Per-session Auto/Manual toggle + manual trading ticket |

## Using it

`dotnet run --project Poink.Sim`, browse to `http://localhost:5001` — the
Simulation page is the app's landing page (`/`, aliased at `/simulation`).

- **Setup** — pick a symbol, a date range, the aggregation toggle (raw
  ticks vs. HLOC/AVDT bars) and bar size, a Brain, trader count, and
  starting cash, then **Load Simulation**.
- **Playback** — Play/Pause, Step (paused-only), a +/- speed stepper
  cycling through presets (0.1x–10000x), an uncapped Max button, Reset
  (rewind, same config), New Simulation (back to setup), Hide/Show Chart.
- **Per-trader controls** — switch any session between Auto and Manual on
  its trader card; Manual exposes a trading ticket (order type, trigger
  price, capital fraction, SL/TP, Buy/Sell/Close).
- **Max** — runs the backend at uncapped speed; combine with Hide Chart for
  very large runs where redrawing the chart is the actual bottleneck.

## Changelog

- **v1.0** — 2026-07-27 — Split out of the monolithic root README into its
  own doc, updated for the current `Services/Simulation`/`Trader`/
  `Repository` file layout, multi-trader sessions, and manual trading mode.
