# Poink.Sim Glossary

Definitions for domain and codebase terminology used in Poink.Sim. Intended for both
human and agent lookup — if you hit an unfamiliar term while reading code, docs, or
the UI in this project, check here first. See also [README.md](README.md) for prose
design context; this file is the flat reference index.

Each entry: **Term** — definition. `File path(s)` where the concept is defined.

## Trading & Domain Concepts

- **Aggregation toggle** — `SimulationConfig.Aggregate`: whether the Brain observes raw
  ticks or completed HLOC/AVDT bars. Controls not just chart display but how often
  `Brain.Decide`/`Trader.Apply` run (once per tick vs. once per completed bar).
  `Models/SimulationSnapshot.cs`
- **Capital fraction** — `SimulationDecision.CapitalFraction` (0..1): fraction of
  current Cash committed when opening a position; ignored for Hold/Close. Fixed at 0.5
  for both neural brains in v1 (no continuous position sizing yet).
  `Models/SimulationDecision.cs`
- **CFD-style account** — The account model this simulator implements: simple cash
  P&L with no leverage/margin, where shorting is a first-class case (not a
  borrow-based short). See README "What this feature is."
- **Distance** — In `HlocAvdtBar`, the sum of absolute tick-to-tick mid-price moves
  within a bar (path length actually traveled), distinct from the simple High−Low
  range. `Models/HlocAvdtBar.cs`
- **Drawdown / MaxDrawdown** — `_peakEquity - state.Equity`, tracked incrementally in
  `Trader.Snapshot`; surfaced by `TradeStatistics.MaxDrawdown`. `Trader/Trader.cs`
- **Equity** — `Cash + UnrealizedPnl`; the account's total mark-to-market value.
  `Models/SimulationAccount.cs`
- **HLOC/AVDT bar** — This project's bar shape: High/Low/Open/Close plus Average
  price, tick-count Volume, path-length Distance, and Up/DownVolume tick-rule split.
  Poink.Sim's own duplicated copy of Poink.ML's bar shape (kept independent so this
  project's build/deploy doesn't depend on Poink.ML). `Models/HlocAvdtBar.cs`
- **Mark-to-market** — Re-pricing an open position's Unrealized P&L against the
  current price without closing it. `Trader.Snapshot(currentPrice)` runs every raw
  tick regardless of the aggregation toggle, so Capital/Position stay live between
  actual decision points. `Trader/Trader.cs`
- **Mid price** — `(Bid + Ask) / 2.0`. Used throughout as "the" price since trades
  execute at mid — spread is not modeled yet.
- **Point of Control (POC)** — The price level with the most volume in a volume
  profile window; computed by `VolumeProfileStats.Compute` and shown both as the
  highlighted row in `TickVolumeProfileChart` and as a projected line on the main
  chart (`ProfilerPage.ProfileLevelLines`). `Components/Charts/VolumeProfileStats.cs`
- **Position** — One open trade: `readonly record struct Position(PositionDirection,
  Size, EntryPrice, EntryTime, StopLossPrice?, TakeProfitPrice?)`. v1 allows only one
  open position at a time (no pyramiding); reversing direction requires an explicit
  Close first. `Models/SimulationAccount.cs`
- **Realized P&L / Unrealized P&L** — Realized: profit/loss booked on Close.
  Unrealized: `Position.UnrealizedPnl(currentPrice)`, live mark-to-market while a
  position is open. `Models/SimulationAccount.cs`
- **Spread / commission** — Not yet modeled; trades execute at mid price.
  `Tick.Bid`/`Ask` already exist so real spread cost can be wired in later.
- **Stop-loss / take-profit (SL/TP)** — `Position`/`SimulationDecision` already carry
  `StopLossPrice`/`TakeProfitPrice`; `Trader.Apply` has a marked-but-unimplemented
  `// SL/TP CHECK` hook where auto-close logic will slot in. `Trader/Trader.cs`
- **Tick** — Raw `(Timestamp, Bid, Ask)` market data point, written into one Postgres
  table per symbol (`ticks_<symbol>`) by the Poink.Trader cAlgo robot.
  `Repository/ITickRepository.cs`
- **Tick rule** — Uptick/downtick classification (mid price vs. previous mid) used to
  split `UpVolume`/`DownVolume` in bars and in `BarPriceHistogram`. Zero-change ticks
  count toward neither, so `UpVolume + DownVolume` can undershoot total `Volume`.
- **Time in market** — `TradeStatistics.TimeInMarket` / `.TimeInLong` / `.TimeInShort`:
  summed trade durations, split by direction.
- **Trade** — One closed round-trip: `record Trade(Id, Direction, Size, EntryPrice,
  EntryTime, ExitPrice, ExitTime, RealizedPnl, Reason)`, appended to `Trader.TradeLog`
  on Close. `Models/SimulationAccount.cs`
- **TickVolume / Volume** — In this app, "volume" is a tick count, not a
  traded-contract count (raw ticks carry no trade-volume field). `HlocAvdtBar.Volume`
  is literally the number of ticks in the bar.
- **Value Area (VA) / Value Area High/Low (VAH/VAL)** — The tightest price band
  holding 70% of a volume profile window's total volume, expanded outward one bucket
  at a time from the Point of Control; VAH/VAL are that band's top/bottom edges.
  `Components/Charts/VolumeProfileStats.cs`
- **Volume Profile** — The tick-count-by-price-level histogram shown by
  `TickVolumeProfileChart`: how much activity occurred at each price level within the
  current window, split into up/down-tick sub-bars. Its Point of Control/Value Area
  are highlighted directly in the panel and projected onto `ProfilerPage`'s main
  chart as POC/VAH/VAL lines. `Components/Charts/TickVolumeProfileChart.razor.cs`
- **VWAP (Volume-Weighted Average Price)** — Cumulative `Σ(Average × Volume) /
  Σ(Volume)` over a bar window, using `HlocAvdtBar.Average`/`Volume` directly (no
  histogram data needed); rendered on `ProfilerPage`'s main chart as a native
  lightweight-charts `LineSeries`, narrowed to the active Volume Range selection if
  one is set. `Components/Pages/Profiler/ProfilerPage.razor.cs` (`VwapPoints`)
- **Win rate** — Percentage of episodes/trades with a positive reward/PnL. Used at
  trade-level (`TradeStatistics`) and episode-level (`RlTrainingService`,
  `TrainLiveTab`).

## Reinforcement Learning Concepts

- **Baseline (mean-centered)** — Variance-reduction technique: returns are normalized
  as `(return - mean) / (std + epsilon)` before use in the loss, instead of a separate
  learned value network. `Training/PolicyGradientTrainingStrategy.cs`
- **Discount factor (γ)** — `DiscountFactor = 0.99`; weights how much future reward
  counts toward a step's discounted return. `Training/PolicyGradientTrainingStrategy.cs`
- **Episode** — One bounded practice run of `episodeBars` bars (each `barWidthMinutes`
  wide) through one Trader, recorded as an `RlEpisode` (list of `RlStep`s plus final
  capital state). `RlEpisodeRunner.Run` stops once that many bars complete, regardless
  of how many ticks were fetched to get there. `Training/RlEpisode.cs`
- **Policy** — The Brain's decision function; "policy gradient" methods adjust network
  weights to increase the probability of actions that led to higher reward.
- **REINFORCE** — The concrete RL algorithm implemented by
  `PolicyGradientTrainingStrategy`: discounted reward-to-go with a mean-centered/
  std-normalized baseline; loss = `-mean(logProb_t * normalizedReturn_t)`.
  `Training/PolicyGradientTrainingStrategy.cs`
- **Reinforcement learning (RL)** — Training paradigm where a Brain (policy) plays
  simulated trading episodes and learns from its own trading outcomes rather than
  labeled historical data (contrast with Poink.ML's supervised training elsewhere).
- **Reward** — Per decision point, the trader's equity drift since the previous
  decision point (mark-to-market P&L from price movement while holding, plus whatever
  a Close realizes) — dense and available every step, not just at episode end.
  `Training/RlEpisodeRunner.cs`
- **Reward-to-go** — Per-step discounted sum of all future rewards in an episode,
  computed backward from the episode's end.
  `Training/PolicyGradientTrainingStrategy.cs` (`TrainEpisode`)

## Core Architecture — Classes & Interfaces

- **`BrainOption`** — One entry in the brain picker: `(Name, IsNeuralNetwork)`, lets
  the UI separate trained/trainable brains from stateless heuristic ones without
  knowing about `IPolicyGradientBrain`. `Training/RlTrainingService.cs`
- **`DummyTickGenerator`** — Static generator of synthetic even-interval random-walk
  ticks with a fixed small spread; shared by "Load Dummy Data" (Simulation) and
  headless RL episodes (Training), so both work without Postgres.
  `Simulation/DummyTickGenerator.cs`
- **`IBrainRepository` / `BrainRepository`** — Persists an `IPolicyGradientBrain`'s
  exported weights (TorchSharp state-dict bytes) plus the training run's
  strategy/parameters/metrics into its own Postgres database (`Poink.Sim`, separate
  from the `Poink.MarketData` tick database). Every save inserts a new snapshot row
  (never upserts), so training history stays browsable. `Repository/IBrainRepository.cs`
- **`IPolicyGradientBrain`** — Sibling interface (not a replacement) to
  `ISimulationBrain`, for trainable stochastic-policy networks. Adds `Network` (live
  `Module<Tensor,Tensor>`), `Encode`, `DecideWithLogProbability`, `DescribeInputs`,
  plus default-implemented `ExportWeights`/`ImportWeights`/`WeightCount`/
  `MoveToTrainingDevice`/`MoveToInferenceDevice`/`DescribeNetworkShape`.
  `Brain/Neural/IPolicyGradientBrain.cs`
- **`IRlTrainingStrategy`** — The "coach" abstraction: `Name`/`CanTrain`/
  `CreateOptimizer`/`TrainEpisode`. Kept stateless like Poink.ML's brain interfaces;
  `RlTrainingService` owns the optimizer's lifetime across a whole run so Adam's
  momentum persists correctly across episodes. `Training/IRlTrainingStrategy.cs`
- **`ISimulationBrain`** — Minimal trading-decision contract: `Name`,
  `Decide(observation)`, `Reset()`. Deliberately excludes `Build`/`BuildInput`/
  TorchSharp `Module` so a stateless heuristic brain has nothing extra to carry.
  `Brain/ISimulationBrain.cs`
- **`ITickRepository` / `TickRepository`** — Postgres tick data access. `ReadTicks` is
  a lazy forward-only iterator (Dapper `buffered: false`); `ReadWindow` is a bounded,
  chunk-pruned window read for one RL episode; `CountTicks` is a cheap chunk-pruned
  `COUNT(*)` for progress bars; `ListTickTables`/`RefreshTickTableStats` manage a
  cached `tick_table_stats` registry table so full-table scans only happen on an
  explicit "Recount," not every page load. `Repository/ITickRepository.cs`
- **`LstmPolicyBrain`** — Policy-gradient brain: same feature contract as
  `MlpPolicyBrain` but the 20-value price window is fed through an `LSTM` (hidden
  size 64, 2 layers) as a true sequence rather than flattened; final hidden state
  concatenated with capital scalars into a small MLP head. Each `Decide()` starts the
  LSTM from a fresh zero hidden/cell state (no state carried across ticks).
  `Brain/Neural/LstmPolicyBrain.cs`
- **MarketSim** — README's name for the tick-streaming subsystem (not a standalone
  class): streams historical ticks for a chosen symbol/date-range from Postgres via a
  live, forward-only `ITickRepository.ReadTicks` reader as playback advances.
- **`MlpPolicyBrain`** — First concrete trained network: a small MLP
  (`Linear(24,32) → Tanh → Linear(32,16) → Tanh → Linear(16,4)`) over a 20-tick/bar
  percent-change price window plus 4 bounded capital/position ratios, one logit per
  `SimulationAction`. Weights persist across runs (built once, mutated in place by
  training). `Brain/Neural/MlpPolicyBrain.cs`
- **`MomentumThresholdBrain`** — Heuristic, rule-based brain: opens/holds a position
  when price moves beyond `±ThresholdPercent` vs. `Lookback` observations ago, closes
  when momentum reverts toward flat (within `RevertPercent`). Stateless, deterministic.
  `Brain/MomentumThresholdBrain.cs`
- **`PolicyGradientTrainingStrategy`** — v1's concrete `IRlTrainingStrategy`: REINFORCE
  with discounted reward-to-go and a mean-centered baseline; uses `optim.Adam`.
  `Training/PolicyGradientTrainingStrategy.cs`
- **`RandomBrain`** — Trivial baseline brain: each decision point rolls a probability
  (`ActionProbability`) to act at all, then rolls direction; the only brain with
  randomness, so it's the one where multiple traders sharing the same instance
  actually diverge. `Brain/RandomBrain.cs`
- **`RandomWindow` / `RandomEpisodeWindow`** — Picks a random start timestamp within a
  table's known range and reads the ticks spanning `fetchSpan` (`barWidthMinutes ×
  (episodeBars + 1)`) from there per training episode via `ITickRepository.ReadTicks`,
  so successive episodes train against different real market windows without holding
  the full range in memory; retries a few times on landing in an empty gap (e.g. a
  weekend) before falling back to the range start. `Training/RlTrainingService.cs`
- **`RingBuffer<T>`** — Fixed-capacity circular buffer implementing
  `IReadOnlyList<T>`, most-recent-last; O(1) `Add` (vs. O(capacity) for
  `List.RemoveAt(0)`), used for the engine's bounded recent-ticks/bars/log windows.
  `Simulation/RingBuffer.cs`
- **`RlEpisodeRunner`** — Static, headless single-episode playback: mirrors
  `SimulationEngine`'s tick loop (one Trader, aggregate-or-raw observation building)
  but with no Timer/locking/UI publish, so training can blast through many episodes
  fast. Data-source agnostic (takes an already-materialized tick list).
  `Training/RlEpisodeRunner.cs`
- **`RlTrainingService`** — Background training orchestrator DI singleton with the
  same `_lock`/`IsRunning`/`Error`/`OnChanged` shape as `SimulationEngine` and every
  Poink.ML `*TrainingService`. Drives `RlEpisodeRunner` in a tight loop, keeps a
  bounded rolling log (`RecentEpisodes`, last 500) and restores/persists brain weights
  via `IBrainRepository`. `Training/RlTrainingService.cs`
- **`SimulationBarAggregator`** — Stateful incremental tick→HLOC/AVDT bar aggregator:
  "feed one tick, get zero-or-one completed bar back," since a sim advances
  tick-by-tick (unlike Poink.ML's batch/whole-list aggregators). Per-tick math
  mirrors Poink.ML's `HlocAvdtAggregator.Aggregate` exactly.
  `Simulation/SimulationBarAggregator.cs`
- **`SimulationEngine`** — The orchestrating DI singleton: wires MarketSim (tick
  reader) + `SimulationBarAggregator` + Brain + one-or-more `Trader`s, driven by a
  self-rescheduling `System.Threading.Timer` for play/pause/step/speed/Max playback.
  Holds one shared in-memory "current run" for the whole process (not per-user/
  session), matching Poink.ML's `*TrainingService` singleton convention.
  `Simulation/SimulationEngine.cs`
- **`SystemPerformanceMonitor`** — Singleton sampling host-machine CPU/RAM/GPU once
  per second (Windows-only, via `PerformanceCounter`/`GlobalMemoryStatusEx`), feeding
  the layout's `PerformanceMonitor` display. `Diagnostics/SystemPerformanceMonitor.cs`
- **`Trader`** — Applies one `SimulationDecision` at a time to Cash/Position; computes
  fills (`Open`/`Close`), tracks `TradeLog`, `MaxEquity`, `MaxDrawdown`. One instance
  per trader session in a run. `Trader/Trader.cs`
- **`TraderSession`** — One configured trader's slice of a shared run: its own
  `Trader`/recent-log buffer, all sharing the single `_activeBrain` instance for the
  run. Private nested class in `Simulation/SimulationEngine.cs`.

## Data Shapes

- **`BarPriceHistogram`** — Sparse per-bar, per-price-bucket tick-count breakdown
  (with up/down split), quantized to a shared `BucketWidth` so multiple bars'
  histograms can be summed bucket-for-bucket (`Merge`) to build a volume profile over
  an arbitrarily long window without retaining raw ticks. `Models/BarPriceHistogram.cs`
- **`BrainTrainingParameters`** — Free-form training-run parameters (`EpisodeCount`,
  `EpisodeBars`, `BarWidthMinutes`, `StartingCash`, `LearningRate`, `LookbackBars`)
  saved alongside a brain snapshot; plain settable properties (not a record) so old
  JSON blobs still deserialize after new fields are added. `Repository/IBrainRepository.cs`
- **`CapitalState`** — Read-only snapshot handed to the Brain/UI: `(Cash,
  RealizedPnl, UnrealizedPnl)`, with `Equity => Cash + UnrealizedPnl`.
  `Models/SimulationAccount.cs`
- **`ChartLinePoint`** — One `(TimeUtc, Price)` point of a computed overlay line (e.g.
  VWAP) on `DrawableOhlcvChart` — a per-bar time series, unlike the single fixed price
  of `ChartHorizontalLine`, so it renders as its own native LineSeries rather than one
  of the click-to-draw primitives. `Components/Charts/DrawableOhlcvChart.razor.cs`
- **`NetworkLayerShape`** — One "layer" description for `NetworkEmulator`'s animation:
  a real magnitude (Linear's `out_features` or LSTM's hidden size), not a literal
  one-dot-per-unit count; `IsRecurrent` flags an LSTM so the animation can draw it
  differently. `Brain/Neural/IPolicyGradientBrain.cs`
- **`RlInputFeature`** — One named value out of a policy brain's `Encode()` input,
  purely for the Train page's "what goes in" display panel — never used for inference
  itself. Record `(Label, Value)`. `Brain/Neural/IPolicyGradientBrain.cs`
- **`SavedBrainSummary`** — Metadata-only view of one saved brain snapshot (everything
  except the weight bytes themselves), used to populate `SavedBrainsTable` without
  pulling megabytes of weights per row. `Repository/IBrainRepository.cs`
- **`SimulationProgress`** — Cheap sibling of `SimulationSnapshot`: just
  cursor/total/timestamp/range, published far more often during an uncapped Max run
  so a lightweight component (`SimTimeline`) can track live progress without paying
  for the full snapshot's copies. `Models/SimulationSnapshot.cs`
- **`SimulationSnapshot`** — Immutable read-model published wholesale by
  `SimulationEngine` on every callback (config, playback state, per-trader state,
  recent bars/ticks/histograms) — what the page renders from.
  `Models/SimulationSnapshot.cs`
- **`VolumeProfileLevels`** — `(Poc, ValueAreaHigh, ValueAreaLow)`: the computed
  Point of Control/Value Area for a volume profile window, from
  `VolumeProfileStats.Compute`. `Components/Charts/VolumeProfileStats.cs`
- **`WeightedPriceSample`** — One price level's contribution to the volume profile:
  `(Price, Count, UpCount, DownCount)`, either a single raw tick (Count=1) or a merged
  multi-tick bucket total.

## Enums & States

- **`CalendarView`** — `Days`, `Months`, `Years`: the three drill-down levels of the
  date picker. Private enum in `Components/Shared/Calendar/Calendar.razor.cs`.
- **`LogTab`** — `Decisions`, `Positions`, `Traders`, `Brain`, `Statistics`: the five
  tabs of the Simulation page's log/detail panel. Private enum in
  `Components/Pages/SimulationPage.razor.cs`.
- **`PositionDirection`** — `Long`, `Short`: direction of an open position; determines
  the sign in `UnrealizedPnl`. `Models/SimulationAccount.cs`
- **Speed presets** — `0.1x, 1x, 10x, 100x, 1000x, 10000x`: the finite playback-speed
  multipliers the +/- stepper cycles through, separate from the uncapped "Max" mode.
  `Components/Pages/SimulationPage.razor.cs` (`Speeds` array)
- **`SimulationAction`** — `Hold`, `EnterLong`, `EnterShort`, `Close`: the four
  decisions a Brain can output; also the 4 output logits of both neural brains.
  `Models/SimulationDecision.cs`
- **`StatEmphasis`** — `None`, `Positive`, `Negative`: display hint for a `StatItem`,
  mapped by `StatisticsPanel` to CSS classes (win/loss coloring).
  `Models/StatGroup.cs`

## UI Components

- **`BrainInfoCard`** — Shows "what's actually running" for a live brain (name,
  NN-or-not, and its `ActiveSnapshot` if the live weights match a saved one) on
  `SimulationPage`'s BRAIN tab. `Components/Pages/Training/Components/BrainInfoCard/`
- **`BrainSelect`** — Brain-only `<select>` + NN-vs-heuristic badge, reused directly
  by `SimulationPage` and by `TrainPage`'s Brain Type section.
  `Components/Pages/Training/Components/BrainSelect/`
- **`Calendar`** — Pure month-grid date picker (Days/Months/Years drill-down + a
  full-year 12-month overlay), no popover of its own — the caller
  (`DateRangeSlider`) owns showing/hiding it. `Components/Shared/Calendar/`
- **`DataTable<TItem>`** — Generic reflection-based table component (paged,
  searchable, sortable, card view, column picker, resizable columns) copied from
  `Poink.Shared.Client` so Poink.Sim can extend it (`CellClass` for PnL sign coloring)
  without touching the shared component other projects depend on.
  `Components/Shared/DataTable/`
- **`DateRangeSlider`** — Dual-thumb date-range picker over a `[Min, Max]` span in
  whole minutes, with week gridlines and popover `Calendar`s for the From/To labels;
  commits on release (`onchange`, not `oninput`) to avoid per-pixel server
  round-trips in Blazor Server. `Components/Shared/DateRangeSlider/`
- **`EpisodeGrid`** — One small colored block per completed training episode,
  wrapping left-to-right, colored by reward sign/magnitude with a redundant `+`/`−`/
  `·` glyph (for CVD accessibility, since the app's success/error color pair fails
  color-only separation). `Components/Pages/Training/Components/EpisodeGrid/`
- **`NetworkEmulator`** — A stylized (not literal) canvas "network activity"
  animation showing the currently-selected brain's real layer shapes
  (`IPolicyGradientBrain.DescribeNetworkShape`) pulsing between idle and busy states
  depending on whether training `IsRunning`. Explicitly not a readout of real
  activations. `Components/Pages/Training/Components/NetworkEmulator/` +
  `wwwroot/js/networkEmulator.js`
- **`OhlcvChart`** — Candlestick chart backed by the vendored TradingView Lightweight
  Charts JS library; shows completed + in-progress bars, live Bid/Ask price lines,
  and a "window start" marker showing how far back the volume-profile window reaches.
  `Components/Charts/OhlcvChart.razor.cs`
- **`PerformanceMonitor`** — Layout-level CPU/RAM/GPU readout, sourced from
  `SystemPerformanceMonitor`. `Components/Layout/PerformanceMonitor.razor.cs`
- **`RewardChart`** — Pure inline-SVG line chart of reward-per-episode, no JS, with a
  zero-baseline reference and native `<title>` hover tooltips.
  `Components/Pages/Training/Components/RewardChart/`
- **`SavedBrainsTable`** — Browsable history of saved brain weight snapshots
  (Use/Delete), shared between `TrainPage` (continue training) and `SimulationPage`
  (pick a trained brain). `Components/Pages/Training/Components/SavedBrainsTable/`
- **`SimTimeline`** — The playback progress rail: self-subscribes to
  `SimulationEngine.OnProgress` directly (not a `[Parameter]`) so it re-renders
  independently of the rest of the page, keeping it live during an uncapped Max run's
  rapid progress-only publishes without forcing a full page re-render.
  `Components/Shared/SimTimeline/`
- **`StatisticsPanel`** — Renders `TradeStatistics.Groups` (a `StatGroup`/`StatItem`
  shape) as labeled sections, used for the Simulation page's Statistics tab.
  `Components/Shared/StatisticsPanel/`
- **`TickDataSource`** — Shared symbol picker + `DateRangeSlider` for a
  `ticks_<symbol>` table; used identically by `SimulationPage` and `TrainPage` so
  both pick historic data the same way. Owns the "table list" fetch + the
  RECOUNT-only refresh button. `Components/Shared/TickDataSource/`
- **`TickVolumeProfileChart`** — Horizontal-bar tick histogram (price on the row
  axis, tick count as bar width), rendered as pure Razor/CSS (no JS, since it's not
  pixel-synced to `OhlcvChart`'s zoom/pan). Consumes either raw `Prices` (tick mode)
  or pre-aggregated `WeightedSamples` (bar mode, via `BarPriceHistogram.Merge`). This
  *is* the app's "Volume Profile" concept.
  `Components/Charts/TickVolumeProfileChart.razor.cs`
- **`TrainLiveTab`** — The Train page's "Live" content: stat tiles, `EpisodeGrid`,
  `RewardChart`, `NetworkEmulator`, action-distribution bars, and the "what goes in"
  inputs panel (`PriceWindowFeatures`/`ScalarFeatures` split from
  `RlInputFeature.Label`). `Components/Pages/Training/Pages/Train/TrainLiveTab.razor.cs`

## Data / Repository Concepts

- **Chunk-pruned query** — A `WHERE timestamp BETWEEN ...` query that only scans the
  TimescaleDB hypertable chunks overlapping the range, instead of the whole table —
  what makes `CountTicks`/`ReadWindow` cheap regardless of table size (e.g. 161.8M
  rows for `ticks__usndaq100`).
- **Streaming tick reads** — Architectural pattern replacing a full-range in-memory
  `List<Tick>` preload with a live, forward-only `ITickRepository.ReadTicks` reader
  for Simulation, and a per-episode `ReadWindow` call for Training. See
  `TODO/2026-07-21-streaming-tick-reads.md`.
- **`tick_table_stats`** — Postgres registry table caching each `ticks_*` table's row
  count / min timestamp / max timestamp, so pages avoid a full-table scan on every
  load; refreshed only by an explicit "Recount" action (`RefreshTickTableStats`).
- **`ticks_<symbol>` tables** — One Postgres table per symbol, written by the
  Poink.Trader cAlgo robot, with an identical `(timestamp, bid, ask)` shape; the raw
  tick data source for both Simulation and Training.
- **TimescaleDB / hypertable** — The Postgres extension that turns `ticks_*` tables
  into chunked hypertables, making bounded/range queries cheap without materializing
  the whole table. See `TODO/2026-07-21-streaming-tick-reads.md`.
- **`trained_brains` table** — Postgres table (in the separate `Poink.Sim` database)
  storing saved brain snapshots: name, brain/strategy names, dataset description,
  JSON training parameters, weight count, average reward, and the
  TorchSharp-serialized weights (`bytea`).

## Acronyms & Abbreviations

- **Adam** — The Adam optimizer (`TorchSharp.torch.optim.Adam`) used by
  `PolicyGradientTrainingStrategy.CreateOptimizer`; its momentum/variance state is
  why the optimizer is created once per training run, not per episode.
- **cAlgo** — The trading-platform/robot framework; "the Poink.Trader cAlgo robot" is
  the external process that writes ticks into the `ticks_<symbol>` Postgres tables
  (lives outside this project, referenced only in comments).
- **CFD** — Contract for Difference; the account style this simulator models (cash
  P&L, first-class shorting, no borrowing).
- **CUDA** — The GPU compute backend (`TorchSharp-cuda-windows` package);
  `IPolicyGradientBrain.MoveToTrainingDevice` picks CUDA if `cuda.is_available()`,
  else CPU.
- **CVD** — Color Vision Deficiency; referenced in `EpisodeGrid`'s doc comment re: why
  reward sign is never color-only.
- **DI** — Dependency Injection (ASP.NET Core's built-in container, used throughout
  `Program.cs` for singleton registrations).
- **DQN** — Deep Q-Network; mentioned in comments as a hypothetical *future*
  value-based strategy (not implemented) that would need its own sibling interface
  alongside `IPolicyGradientBrain`.
- **γ (gamma)** — Discount factor symbol used in RL return calculations
  (`DiscountFactor = 0.99`).
- **HLOC/AVDT** — High/Low/Open/Close + Average/Volume/Distance/Time: this project's
  bar shape name (`HlocAvdtBar`).
- **LSTM** — Long Short-Term Memory (recurrent network); `LstmPolicyBrain`'s network
  shape.
- **MLP** — Multi-Layer Perceptron; `MlpPolicyBrain`'s network shape.
- **NN** — Neural Network (e.g. `IsNeuralNetwork` on `BrainOption`).
- **P&L** — Profit and Loss (realized/unrealized).
- **REINFORCE** — The specific policy-gradient RL algorithm (Monte Carlo policy
  gradient) implemented in `PolicyGradientTrainingStrategy`.
- **RL** — Reinforcement Learning.
- **SL/TP** — Stop-Loss / Take-Profit; marker comment (`// SL/TP CHECK`) in
  `Trader.Apply` for the not-yet-implemented auto-close hook.
- **WDDM** — Windows Display Driver Model; the GPU driver model needed for the GPU
  performance-counter categories `SystemPerformanceMonitor` reads.
