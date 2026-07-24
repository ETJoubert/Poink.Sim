# Simulation — interactive trader replay

> Status: **in progress** — heuristic placeholder brains (`MomentumThresholdBrain`,
> `RandomBrain`) plus a first trained brain (`MlpPolicyBrain`, trained by a
> reinforcement-learning strategy against simulated episodes, see "Machine-learning
> brain" below); the Trader is now a leveraged CFD account (margin, stop-out,
> spread, commission, SL/TP, Limit/Stop pending orders) -- still no multiple
> concurrent positions/pyramiding.

## What this feature is

A live, interactive replay of historical ticks driven by three pieces:

- **MarketSim** — streams historical ticks for a chosen symbol and date range from
  Postgres (`ITickRepository`), read live off a forward-only reader as playback
  advances under user control (play/pause/step/speed), either raw or aggregated
  into HLOC/AVDT bars.
- **Brain** (`ISimulationBrain`) — observes the stream (raw ticks or completed bars,
  whichever the aggregation toggle selects) plus the current Capital/Position, and
  decides whether to hold, enter long/short, or close.
- **Trader** — applies the Brain's decisions, tracking Capital and one open Position
  (plus at most one resting pending order) at a time, under leveraged CFD
  mechanics: margin sizing, spread-correct fills (buy at Ask, sell at Bid),
  pluggable commission, and automatic SL/TP/margin-stop-out closes. This is a
  CFD-style account, so shorting is a first-class case, not a borrow-based short.

## Why this is its own project

Poink.Sim was split out of Poink.ML because simulation playback isn't Poink.ML's
training pipeline — it's a separate app with its own lifecycle (play/pause/step/
speed against live Capital/Position). It does carry its own TorchSharp/Microsoft.ML
(now used by a trained `ISimulationBrain`, see "Machine-learning brain" below) plus
Postgres tick access and the shared Blazor terminal UI (`Poink.Shared.Client`), but
never a `ProjectReference` to Poink.ML itself: `ITickRepository`/`TickRepository`
and the `HlocAvdtBar` bar shape are each duplicated here rather than shared, so
this project's build/deploy stays independent of Poink.ML's.

## Playback design

`SimulationEngine` plays a run back off a live, forward-only `ITickRepository.ReadTicks`
reader instead of materializing the selected range into memory -- playback only ever
moves forward (Play/Step/speed all advance the same reader), so a pausable/steppable/
speed-controlled sim never actually needs random access. `Reset` disposes the current
reader and opens a fresh one from the start of the configured range; the progress bar's
total comes from a single upfront chunk-pruned `COUNT(*)`, not the reader's length.
`TryLoadDummy`'s synthetic ticks are a small in-memory list instead (no Postgres
involved), wrapped behind the same reader interface so the playback/reset code doesn't
need to know which source it's replaying.

Playback runs on one `System.Threading.Timer` per engine, used as a **self-rescheduling
one-shot** (period = `Infinite`; the callback re-arms itself at the end) so callbacks
never overlap and Pause/SetSpeed take effect cleanly at a callback boundary. The speed
multiplier controls **how many ticks advance per callback**, not how fast the timer
fires (fixed ~100ms cadence) — this bounds UI update traffic to ~10/sec regardless of
speed, so high multipliers play through history faster without flooding the page.

`SetMaxSpeed()` ("Max" on the page) is a separate **uncapped** mode, not just a
bigger multiplier: `TimerCallback` drains ticks for a fixed time slice
(`UncappedSliceMs`) instead of a bounded tick count, immediately rescheduling for
the next slice — as fast as the reader/brain allow overall, but still publishing a
snapshot roughly every slice so the page (timeline, stats) visibly progresses
instead of freezing until one final publish. Hiding the chart/tick table for faster
rendering during a Max run is a separate, independent page-side toggle (Hide Chart),
since the engine has no concept of "hidden."

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

## Machine-learning brain (reinforcement learning)

**TLDR:** the brain used to be hand-written if/else rules. Now there's also a brain
that's a small neural network — it starts out making random trades, plays thousands
of practice rounds against fake price data, and after each round nudges its own
weights toward whatever decisions made money and away from whatever lost money. No
historical "correct answer" data involved (that's what makes it reinforcement
learning instead of the supervised training Poink.ML does elsewhere) — it only ever
learns from its own simulated trading outcomes. The Train page (`/train`) runs these
practice sessions, with a Live tab showing reward improving (or not) over time.

The abstraction underneath:

| Role | Interface | Analogy |
|---|---|---|
| A trader | `ISimulationBrain` | "someone who makes trading decisions" — a fixed rulebook or a trained network, the simulator doesn't care which |
| A trainable trader | `IPolicyGradientBrain` | a trader who can also say *why* they made a choice (a confidence score) — the one extra thing a coach needs |
| A coach | `IRlTrainingStrategy` | "a method of turning practice results into improvement" — one coaching style today (REINFORCE); a different one can be swapped in later without touching the trader or the practice-round logic |

Everything else (`RlEpisodeRunner`, `RlTrainingService`) just wires a trader and a
coach together and doesn't need to know which specific one it's holding — that's the
whole point of splitting them into separate, swappable interfaces.

`ISimulationBrain` was deliberately kept minimal (`Name`/`Decide`/`Reset`, no
`Build`/`BuildInput`, no TorchSharp `Module`) so a heuristic brain has nothing to
carry — but that same minimalism is exactly what let a trained brain implement it by
holding a loaded `Module<Tensor,Tensor>` privately. That's now real, not just a
design placeholder:

- **`IPolicyGradientBrain`** (`Brain/Neural/`) is a sibling to `ISimulationBrain`, not
  a replacement — same pattern Poink.ML uses to keep each model family behind its own
  narrow interface (`IForecastModel`/`IQuantileBrain`/`IPatternBrain`) instead of one
  do-everything abstraction. It adds `Network` (the live module), `Encode` (this
  brain's own fixed-width feature encoding — the only data path in, so it only ever
  sees what `SimulationObservation` already exposes, no lookahead), and
  `DecideWithLogProbability` (forward pass + sampled action + that action's
  log-probability, which a training strategy needs and plain playback doesn't).
- **`MlpPolicyBrain`** is the first concrete network: a small MLP over the last 20
  ticks/bars (as percent-change-from-current-price) plus four bounded capital/position
  ratios (position direction, unrealized/realized PnL over equity, cash over equity),
  outputting one logit per `SimulationAction`. Built once in the constructor and kept
  alive for the object's lifetime — weights persist across simulation runs, and
  training mutates this exact instance in place, so a brain plugged into a live
  Simulation run visibly reflects whatever it has learned so far. `CapitalFraction` is
  fixed at 0.5 for v1 (discrete action space only, no continuous position sizing yet).
- **Training is reinforcement learning by actually trading, not supervised learning
  on historical/labeled data** (unlike everything in Poink.ML). `RlEpisodeRunner`
  (`Training/`) headlessly plays one episode against an already-materialized tick
  list — no Timer, no locking, no UI publish, since training needs to blast through
  many episodes fast. It doesn't care where the ticks came from; `RlTrainingService`
  hands it either synthetic ticks (`Simulation/DummyTickGenerator.cs`, shared with the
  live "Load Dummy Data" path) or a slice of real historic ticks. For a historic run,
  the full table/date-range is loaded once from Postgres up front (like
  `SimulationEngine.TryLoad`), and each episode trains against a random contiguous
  window of that range (`RlTrainingService.RandomWindow`) — one DB query per run, not
  per episode, and every episode still sees a different slice of real market data.
  Reward per decision point is the trader's equity drift since the *previous* decision
  point (mark-to-market P&L from price movement while holding a position, plus
  whatever a Close realizes) — dense and available every step, not just at episode end.
- **`IRlTrainingStrategy`** (`Training/`) is the "different training strategies" seam:
  `Name`/`CanTrain`/`CreateOptimizer`/`TrainEpisode`, kept stateless like Poink.ML's
  brain interfaces — `RlTrainingService` owns the optimizer's lifetime across a whole
  run (so Adam's momentum persists correctly across episodes instead of resetting
  every one), the strategy just turns one recorded episode into one gradient step.
  Adding a new algorithm (e.g. a future DQN/actor-critic strategy) means one class + one
  DI registration line, same as adding a new brain.
- **`PolicyGradientTrainingStrategy`** is v1's concrete strategy: REINFORCE with
  discounted reward-to-go (`γ = 0.99`) and a mean-centered baseline (simple variance
  reduction, no separate value network) — loss is `-mean(logProb_t * normalizedReturn_t)`.
- **`RlTrainingService`** is a DI singleton with the same `_lock`/`IsRunning`/`Error`/
  `OnChanged` shape as every other `*TrainingService` in Poink.ML and as
  `SimulationEngine` itself, but drives `RlEpisodeRunner` in a tight background loop
  instead of a UI-paced Timer. It also keeps a bounded rolling log (`RecentEpisodes`,
  last 500 episodes) of every completed episode's reward and final equity — the
  toggleable "visual feedback" the Train page renders as a table so a run can actually
  be evaluated against something, not just a single running-average number.
- **The Train page** (`/train`) has two tabs — **Setup** picks a brain + strategy
  (only pairings where `CanTrain` is true are trainable), episode count, ticks per
  episode, starting cash, and learning rate, then **Start Training**; **Live** is the
  toggle for visual feedback (see "Live training visuals" below) and is where the
  page jumps automatically once a run starts.

Known v1 limitations, deliberately not solved yet: **no weight persistence** (trained
weights live only for the process's lifetime, matching Poink.ML's own gap — TorchSharp's
`Module.save`/`load` is the natural follow-up); **fixed capital fraction** (no
continuous position sizing); **dense per-tick reward only** (no reward shaping — e.g.
drawdown penalty, transaction cost); **one brain trains at a time** (`IsRunning` guards
against concurrent runs, same as every other `*TrainingService`).

## Live training visuals

The Live tab exists so a training run doesn't just look like a progress bar ticking
up — it shows what's actually happening, and (via a new "what goes in" panel) what
the network is literally seeing at the moment it decides.

- **Stat tiles** — episodes done/total, recent average reward, best/worst episode,
  win rate (% of episodes with positive reward). All computed from
  `RlTrainingService`'s existing rolling state, no new tracking beyond
  `BestReward`/`WorstReward`/`ActionCounts` added alongside `RecentAverageReward`.
- **`EpisodeGrid`** (`Components/Training/`) — one small block per completed episode
  (`RlEpisodeMetric` in `RlTrainingService.RecentEpisodes`), wrapping left-to-right in
  completion order, colored by reward sign/magnitude. Pure Razor + CSS, no JS — same
  reasoning as `TickVolumeProfileChart`: it doesn't need pixel-sync to anything. Color
  alone isn't trusted for sign here: this app's success/error pair fails CVD
  separation when run through the dataviz skill's validator (same as every other
  red/green PnL indicator already in the app), so every block also carries a `+`/`−`/`·`
  glyph. The latest block pulses while training is running.
- **`RewardChart`** (`Components/Training/`) — pure inline SVG (no JS), a single-series
  line of reward per episode (no legend needed for one series), a subtle zero-baseline,
  and a native `<title>` per point for hover detail. The latest point pulses while
  running.
- **`NetworkEmulator`** (`Components/Training/` + `wwwroot/js/networkEmulator.js`) — a
  canvas animation, JS interop via the same ES-module import/dispose pattern as
  `OhlcvChart`. This is explicitly **not** a literal readout of real activations
  (nothing pipes per-tick internals out of `MlpPolicyBrain` for this) — it's a
  stylized, scaled-down node diagram (not literally 24/32/16/4 nodes) with the output
  layer labeled Hold/Long/Short/Close, that visibly changes between an ambient idle
  state and a busier pulsing/flowing state depending on `IsRunning`. Colors are read
  live via `getComputedStyle` (matching `CandlestickChart.ts`'s `cssVar()` helper), so
  it repaints correctly across the enterprise/synthwave themes too.
- **Action distribution** — a small labeled bar per `SimulationAction`
  (Hold/Long/Short/Close), driven by `RlTrainingService.ActionCounts` (tallied
  incrementally as episodes complete, not recomputed from history).
- **Inputs panel — "what goes in."** `IPolicyGradientBrain` gained a fourth member,
  `DescribeInputs(observation)`, returning a labeled breakdown of `Encode`'s raw
  input (`RlInputFeature(Label, Value)`) — each brain describes its own feature
  layout, the same ownership pattern as `Encode` itself, so a future brain with a
  different input shape just implements this differently. `RlEpisodeRunner` calls it
  once per episode against the *final* decision point (not every tick — aggregated
  enough to show "what goes in" without flooding `OnChanged`), and `RlEpisode` carries
  the result as `LastInputs`. `RlTrainingService.LatestInputs` holds the most recent
  one. The panel renders `MlpPolicyBrain`'s 20-tick price window as a small
  magnitude/sign-colored bar strip (hover for the exact label+value) and its 4
  capital/position ratios as always-labeled readouts (position, unrealized/equity,
  realized/equity, cash/equity).

## Not yet implemented

- **Multiple concurrent positions / pyramiding.** v1 allows only one open position
  (and at most one resting pending order) at a time; reversing direction requires
  an explicit `Close` first.

## Using it

`dotnet run --project Poink.Sim`, browse to `http://localhost:5001` — the Simulation
page is the app's landing page (`/`, aliased at `/simulation`).

- **Setup** — pick a symbol (from the tick tables Postgres has), a date range, the
  aggregation toggle (raw ticks vs. HLOC/AVDT bars) and bar size, a brain, and
  starting cash, then **Load Simulation**.
- **Playback** — Play/Pause, Step (paused-only), a +/- speed stepper cycling through
  presets (0.1x-10000x), an uncapped Max button, Reset (rewind, same config), New
  Simulation (back to setup), Hide/Show Chart (manually skip chart rendering for
  faster trading at high speed). Live: a candlestick chart (aggregation on) or a
  scrolling raw-tick table (aggregation off), a Capital/Position readout (cash,
  realized/unrealized P&L, equity, position), a timeline rail showing where the
  current tick sits between the run's From/To, and a trade/decision log.
- **Max** — runs the backend at uncapped speed (drains every remaining tick in one
  go, see "Playback design" above); combine with Hide Chart for very large runs
  where redrawing the chart is the actual bottleneck, not the simulation math
  itself.
- **Train** (`/train`) — **Setup** tab: pick a trainable brain + strategy, episode
  count, ticks per episode, starting cash, learning rate, then **Start Training**
  (runs headlessly against synthetic ticks, no Postgres needed). **Live** tab
  (jumped to automatically once a run starts): stat tiles, the episode grid, reward
  chart, network activity animation, action distribution, an "INPUTS" panel showing
  what's actually feeding the network, and the episode metrics log. See
  "Machine-learning brain" and "Live training visuals" above.

## Files

Organized by role rather than one flat feature folder:

| folder | file | role |
|---|---|---|
| `Models/` | `HlocAvdtBar.cs` | this project's own copy of Poink.ML's HLOC/AVDT bar shape |
| `Models/` | `SimulationAccount.cs` | `Position`/`CapitalState`/`Trade`/`TradeExecution`/`MaintenanceResult` domain shapes (leveraged CFD account) |
| `Models/` | `SimulationDecision.cs` | `SimulationAction`/`OrderType`/`SimulationDecision`/`SimulationObservation` — the Brain's observe/decide data shapes |
| `Models/` | `SimulationSnapshot.cs` | `SimulationConfig`/`SimulationSnapshot`/`SimulationLogEntry` read-model |
| `Brain/` | `ISimulationBrain.cs` | brain contract |
| `Brain/` | `MomentumThresholdBrain.cs` | heuristic placeholder brain (percent-change threshold) |
| `Brain/Neural/` | `IPolicyGradientBrain.cs` | trained-brain family contract (sibling to `ISimulationBrain`): `Network`, `Encode`, `DecideWithLogProbability`, `DescribeInputs`; also declares `RlInputFeature` |
| `Brain/Neural/` | `MlpPolicyBrain.cs` | first concrete network — small MLP over a price window + capital/position ratios |
| `Trader/` | `Trader.cs`, `TraderSettings.cs` | leveraged CFD account engine (`Apply`/`Advance`) + its settings bundle (leverage/stop-out/commission) |
| `Trader/Commission/` | `ICommissionModel.cs`, `PercentOfNotionalCommission.cs` | pluggable per-fill commission cost |
| `Trader/Orders/` | `PendingOrder.cs` | a resting Limit/Stop entry order |
| `Simulation/` | `SimulationBarAggregator.cs` | incremental (stateful) tick → HLOC/AVDT bar aggregator |
| `Simulation/` | `SimulationEngine.cs` | orchestrating singleton: playback timer + MarketSim + Aggregator + Brain + Trader |
| `Simulation/` | `DummyTickGenerator.cs` | synthetic random-walk ticks, shared by "Load Dummy Data" and `RlEpisodeRunner` |
| `Training/` | `RlEpisode.cs` | `RlStep`/`RlEpisode`/`RlEpisodeMetric`/`RlActionCounts`/`RlDataSourceConfig` — recorded-trajectory, metrics-log, and data-source shapes |
| `Training/` | `RlEpisodeRunner.cs` | headless single-episode playback (one Trader, no Timer/UI); also describes the episode's final inputs |
| `Training/` | `IRlTrainingStrategy.cs` | RL algorithm contract — the "different training strategies" seam |
| `Training/` | `PolicyGradientTrainingStrategy.cs` | v1 strategy: REINFORCE with discounted-return + mean baseline |
| `Training/` | `RlTrainingService.cs` | background training orchestrator singleton + rolling episode-metrics log + `LatestInputs` |
| `Repository/` | `ITickRepository.cs` / `TickRepository.cs` | Postgres tick data access |
| `Components/Pages/` | `SimulationPage.razor(.cs/.css)` | the `/` page (setup form + live playback view) |
| `Components/Pages/` | `TrainPage.razor(.cs/.css)` | the `/train` page (Setup/Live tabs — setup form, stat tiles, live visuals, metrics log) |
| `Components/Training/` | `EpisodeGrid.razor(.cs/.css)` | one block per episode, reward-colored, Razor+CSS only |
| `Components/Training/` | `RewardChart.razor(.cs/.css)` | reward-per-episode line, inline SVG, no JS |
| `Components/Training/` | `NetworkEmulator.razor(.cs/.css)` | ambient "network activity" canvas animation, JS interop |
| `Components/Charts/` | `OhlcvChart.razor(.cs/.css)` | candlestick chart component |
| `Components/Layout/` | `MainLayout.razor`, `SimNavLinks.razor` | app shell + nav |
| `wwwroot/js/` | `networkEmulator.js` | `NetworkEmulator`'s canvas animation (requestAnimationFrame, theme-aware colors) |

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
- [x] `IPolicyGradientBrain` + `MlpPolicyBrain` — first trained brain, MLP over price window + capital/position ratios
- [x] `RlEpisodeRunner` — headless episode playback (`DummyTickGenerator` extracted for reuse)
- [x] `IRlTrainingStrategy` + `PolicyGradientTrainingStrategy` — REINFORCE with discounted-return + mean baseline
- [x] `RlTrainingService` + `TrainPage` (`/train`) — background training loop, progress, toggleable episode-metrics log
- [x] `TrainPage` Setup/Live tabs; stat tiles (best/worst reward, win rate) alongside recent-average
- [x] `EpisodeGrid`/`RewardChart`/`NetworkEmulator` + action-distribution bar — live training visuals
- [x] `IPolicyGradientBrain.DescribeInputs` + `RlEpisodeRunner`/`RlTrainingService` plumbing — "what goes in" inputs panel
- [ ] End-to-end verification against real tick data (build + run + walk through the page)
- [ ] Weight persistence, continuous position sizing, reward shaping, additional RL strategies (see "Machine-learning brain")
- [x] Train against real historical ticks, not just synthetic episodes — `RlTrainingService` loads a table/date-range once from `ITickRepository` and windows it randomly per episode; `TrainPage` toggles dummy vs. historic
- [ ] Swappable reward policy (`IRewardPolicy`, same DI-registered-family pattern as brains/strategies) instead of the hard-coded equity-delta line in `RlEpisodeRunner.RecordStep`
- [x] Full CFD capability — leverage, margin (used/free/level), spread-correct fills, pluggable commission (`ICommissionModel`), margin-call stop-out, and live SL/TP auto-close (`Trader.Advance`, run unconditionally every raw tick)
- [x] Limit/Stop pending entry orders (`SimulationDecision.OrderType`/`TriggerPrice`, `Trader.CurrentPendingOrder`) — see GLOSSARY.md "Pending order (Limit / Stop)"
