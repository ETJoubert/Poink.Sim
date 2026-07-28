# Poink.Sim Glossary

Definitions for domain and codebase terminology used in Poink.Sim. Intended for both
human and agent lookup — if you hit an unfamiliar term while reading code, docs, or
the UI in this project, check here first. See also [README.md](README.md) and
[Docs/](Docs/README.md) for prose design context, organized by area
(Simulation/Training/Brain/Profiler); this file is the flat reference index
spanning all of them.

> For `Brain`/`Brain/Neural` terms specifically, [Brain/README.md](Brain/README.md)
> is authoritative — some entries below (e.g. `MlpPolicyBrain`, `LstmPolicyBrain`
> as standalone classes) describe the pre-composition model and predate it.

Each entry: **Term** — definition. `File path(s)` where the concept is defined.

## Trading & Domain Concepts

- **Aggregation toggle** — `SimulationConfig.Aggregate`: whether the Brain observes raw
  ticks or completed HLOC/AVDT bars. Controls not just chart display but how often
  `Brain.Decide`/`Trader.Apply` run (once per tick vs. once per completed bar).
  Changeable mid-run via `SimulationEngine.SetAggregation` (see below), not just at
  load time. `Models/SimulationSnapshot.cs`
- **Capital fraction** — `SimulationDecision.CapitalFraction` (0..1): fraction of
  current Cash committed when opening a position; ignored for Hold/Close. Fixed at
  0.5 for a brain built without the `Volume` `ActionOutputKind` (see
  `IActionOutputHead`); a brain built with it instead has its network pick one of
  `DiscretizedLevelsActionHead`'s 25%/50%/75%/100% levels per entry.
  `Models/SimulationDecision.cs`
- **CFD-style account** — The account model this simulator implements: leveraged
  margin trading with real spread cost and commission, where shorting is a
  first-class case (not a borrow-based short). See
  [Docs/Simulation.md](Docs/Simulation.md) "What it is."
- **Commission model / `ICommissionModel`** — Pluggable per-fill commission cost:
  `CommissionFor(size, fillPrice)`, charged once per fill leg (open and close are
  separate calls). `PercentOfNotionalCommission` is the only implementation today
  (a flat percent of the fill's notional value); swap in a different schedule
  (flat fee, per-lot) without touching `Trader`. `Trader/Commission/`
- **Distance** — In `HlocAvdtBar`, the sum of absolute tick-to-tick mid-price moves
  within a bar (path length actually traveled), distinct from the simple High−Low
  range. `Models/HlocAvdtBar.cs`
- **Drawdown / MaxDrawdown** — `_peakEquity - state.Equity`, tracked incrementally in
  `Trader.Snapshot`; surfaced by `TradeStatistics.MaxDrawdown`. `Trader/Trader.cs`
- **Equity** — `Cash + UnrealizedPnl`; the account's total mark-to-market value.
  `Models/SimulationAccount.cs`
- **Exit-side pricing** — Unrealized P&L and every fill mark/execute at the side
  of the spread the position would actually exit at right now -- Bid for a Long,
  Ask for a Short -- not mid. `Position.UnrealizedPnl(bid, ask)`.
  `Models/SimulationAccount.cs`
- **HLOC/AVDT bar** — This project's bar shape: High/Low/Open/Close plus Average
  price, tick-count Volume, path-length Distance, and Up/DownVolume tick-rule split.
  Poink.Sim's own duplicated copy of Poink.ML's bar shape (kept independent so this
  project's build/deploy doesn't depend on Poink.ML). `Models/HlocAvdtBar.cs`
- **Leverage** — `TraderSettings.Leverage`: multiplies committed margin into
  notional exposure (`notional = committed × Leverage`). 1 (the default) means no
  leverage -- numerically identical to the pre-CFD sizing every existing brain
  still produces. `Trader/TraderSettings.cs`
- **Margin Used / Margin Free / Margin Level** — `MarginUsed`: locked collateral
  for the open position, `Position.Size × Position.EntryPrice ÷ Leverage`
  (margin-at-entry convention -- static for the life of the position, not
  re-priced as the market moves), 0 while flat. `MarginFree`: `Equity −
  MarginUsed`. `MarginLevel`: `Equity ÷ MarginUsed × 100`, `null` while flat.
  `Models/SimulationAccount.cs` (`CapitalState`)
- **Mark-to-market** — Re-pricing an open position's Unrealized P&L against the
  current price without closing it, at exit-side price. `Trader.Advance` runs every
  raw tick regardless of the aggregation toggle (calling `Trader.Snapshot`
  internally, plus the SL/TP/stop-out/pending-order checks), so Capital/Position
  stay live between actual decision points. `Trader/Trader.cs`
- **Mid price** — `(Bid + Ask) / 2.0`. Still the Brain-facing/charting price
  (`SimulationObservation.CurrentPrice`, the candle series) -- but fills and
  mark-to-market use real `Tick.Bid`/`Ask` (exit-side pricing), not mid.
- **Pending order (Limit / Stop)** — A Brain can request a deferred entry via
  `SimulationDecision.OrderType`/`TriggerPrice` instead of an immediate market
  fill; `Trader.CurrentPendingOrder` holds at most one at a time (mirrors the
  one-position-at-a-time rule). Checked every raw tick by `Trader.Advance`
  against real Bid/Ask, not just once per Brain decision point -- a Buy Limit
  fires when Ask ≤ trigger, Buy Stop when Ask ≥ trigger, Sell Limit when Bid ≥
  trigger, Sell Stop when Bid ≤ trigger, filling at the actual Bid/Ask (so a
  gap-through produces realistic slippage). `Trader/Orders/PendingOrder.cs`
- **Point of Control (POC)** — The price level with the most volume in a volume
  profile window; computed by `VolumeProfileStats.Compute` and shown both as the
  highlighted row in `TickVolumeProfileChart` and as a projected line on the main
  chart (`ProfilerPage.ProfileLevelLines`). `Components/Charts/VolumeProfileStats.cs`
  Time Profile computes its own, time-weighted POC independently via
  `TimeProfileStats.Compute`.
- **Position** — One open trade: `readonly record struct Position(PositionDirection,
  Size, EntryPrice, EntryTime, StopLossPrice?, TakeProfitPrice?)`. v1 allows only one
  open position at a time (no pyramiding); reversing direction requires an explicit
  Close first. `StopLossPrice`/`TakeProfitPrice` are live -- checked every raw tick
  by `Trader.Advance`, force-closing the position at exit-side price when breached.
  `Models/SimulationAccount.cs`
- **Realized P&L / Unrealized P&L** — Realized: profit/loss booked on Close, net of
  the round-trip Commission. Unrealized: `Position.UnrealizedPnl(bid, ask)`, live
  mark-to-market at exit-side price while a position is open.
  `Models/SimulationAccount.cs`
- **Spread / commission** — Fills always execute at the correct side of the real
  `Tick.Bid`/`Ask` (buy at Ask, sell at Bid) -- in both raw-tick and bar-aggregated
  mode, since `SimulationEngine`/`RlEpisodeRunner` thread the triggering tick's real
  spread through regardless of the aggregation toggle. Commission is charged per
  fill leg by the configured `ICommissionModel`. `Trader/Trader.cs`
- **Stop-loss / take-profit (SL/TP)** — `Position`/`SimulationDecision` carry
  `StopLossPrice`/`TakeProfitPrice`; `Trader.Advance` checks them every raw tick
  (at exit-side price) and force-closes the position on breach, SL taking priority
  if both breach the same tick. `Trader/Trader.cs`
- **Stop-out / Margin call** — `Trader.Advance` force-closes the open position when
  Margin Level drops below `TraderSettings.StopOutLevelPercent` (default 50%).
  Checked after the SL/TP check, only while a position remains open.
  `Trader/Trader.cs`
- **Tick** — Raw `(Timestamp, Bid, Ask)` market data point, written into one Postgres
  table per symbol (`ticks_<symbol>`) by the Poink.Trader cAlgo robot.
  `Repository/ITickRepository.cs`
- **Tick rule** — Uptick/downtick classification (mid price vs. previous mid) used to
  split `UpVolume`/`DownVolume` in bars and in `BarPriceHistogram`. Zero-change ticks
  count toward neither, so `UpVolume + DownVolume` can undershoot total `Volume`.
- **Time in market** — `TradeStatistics.TimeInMarket` / `.TimeInLong` / `.TimeInShort`:
  summed trade durations, split by direction.
- **Time Profile** — The dwell-time-by-price-level histogram shown by
  `TickTimeProfileChart`: how many seconds price spent at each price level within the
  current window (vs. Volume Profile's tick *count*), split into up-time/down-time
  sub-bars. Raw-tick mode measures dwell time directly from consecutive tick
  timestamps; bar mode approximates it per bucket from each bar's own average tick
  interval (`BarPriceHistogram.MergeTime`), since bar mode retains no per-tick
  timestamps. Its Point of Control/Value Area are computed by
  `TimeProfileStats.Compute`, independently of Volume Profile's.
  `Components/Charts/TickTimeProfileChart/TickTimeProfileChart.razor.cs`. Also available as a
  trainable brain feature via `TimeProfileEncoder` (see **`IHasLookbackBars`**),
  which re-bins the same `BarPriceHistogram.MergeTime` output/POC/Value-Area
  math (`TimeProfileAnalysis`, the seconds-weighted counterpart to
  `VolumeProfileAnalysis`) into a fixed-width network input instead of a chart.
- **Trade** — One closed round-trip: `record Trade(Id, Direction, Size, EntryPrice,
  EntryTime, ExitPrice, ExitTime, RealizedPnl, Commission, Reason)`, appended to
  `Trader.TradeLog` on Close. `RealizedPnl` is net of `Commission` (the round-trip
  total across both the open and close leg) -- what `TradeStatistics` win/loss
  reads. `Models/SimulationAccount.cs`
- **TickVolume / Volume** — In this app, "volume" is a tick count, not a
  traded-contract count (raw ticks carry no trade-volume field). `HlocAvdtBar.Volume`
  is literally the number of ticks in the bar.
- **Value Area (VA) / Value Area High/Low (VAH/VAL)** — The tightest price band
  holding 70% of a volume profile window's total volume, expanded outward one bucket
  at a time from the Point of Control; VAH/VAL are that band's top/bottom edges.
  `Components/Charts/VolumeProfileStats.cs` Time Profile computes its own,
  time-weighted Value Area independently via `TimeProfileStats.Compute`.
- **Volume Profile** — The tick-count-by-price-level histogram shown by
  `TickVolumeProfileChart`: how much activity occurred at each price level within the
  current window, split into up/down-tick sub-bars. Its Point of Control/Value Area
  are highlighted directly in the panel and projected onto `ProfilerPage`'s main
  chart as POC/VAH/VAL lines. `Components/Charts/TickVolumeProfileChart/TickVolumeProfileChart.razor.cs`
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
  learned value network. `Services/Training/PolicyGradientTrainingStrategy.cs`
- **Discount factor (γ)** — `DiscountFactor = 0.99`; weights how much future reward
  counts toward a step's discounted return. `Services/Training/PolicyGradientTrainingStrategy.cs`
- **Episode** — One bounded practice run of `episodeBars` bars (each `barWidthMinutes`
  wide) through one Trader, recorded as an `RlEpisode` (list of `RlStep`s plus final
  capital state, plus every bar this episode processed -- including leading
  **warm-up bars** (see that entry) -- as `HlocAvdtBar`/`BarPriceHistogram`, plus,
  for only the *decision* bars and only for an `IPolicyGradientBrain`, each
  decision's `DescribeInputs` snapshot and its pre-Apply equity anchor).
  `RlEpisodeRunner.Run` starts recording `RlStep`s/rewards only after warm-up
  completes (`Bars`/`BarHistograms` cover warm-up + decisions, `BarInputs`/
  `BarEquity`/`BarOutputs` cover decisions only, offset from `Bars` by
  `RlEpisode.WarmupBars`), then stops once `episodeBars` *decision* bars complete,
  regardless of how many ticks were fetched to get there. `Services/Training/RlEpisode.cs`
- **Policy** — The Brain's decision function; "policy gradient" methods adjust network
  weights to increase the probability of actions that led to higher reward.
- **REINFORCE** — The concrete RL algorithm implemented by
  `PolicyGradientTrainingStrategy`: discounted reward-to-go with a mean-centered/
  std-normalized baseline; loss = `-mean(logProb_t * normalizedReturn_t)`.
  `Services/Training/PolicyGradientTrainingStrategy.cs`
- **Reinforcement learning (RL)** — Training paradigm where a Brain (policy) plays
  simulated trading episodes and learns from its own trading outcomes rather than
  labeled historical data (contrast with Poink.ML's supervised training elsewhere).
- **Reward** — Per decision point, whatever the training run's selected `IRewardPolicy`
  computes (see that entry) — dense and available every step, not just at episode end.
  The default (`EquityDriftRewardPolicy`) is the trader's raw equity drift since the
  previous decision point (mark-to-market P&L from price movement while holding, plus
  whatever a Close realizes); `TradingRulesRewardPolicy` shapes that same base figure
  with loss-aversion/anti-churn/max-holding-time terms. `Services/Training/RlEpisodeRunner.cs`
- **Reward-to-go** — Per-step discounted sum of all future rewards in an episode,
  computed backward from the episode's end.
  `Services/Training/PolicyGradientTrainingStrategy.cs` (`TrainEpisode`)
- **Warm-up bars** — `IPolicyGradientBrain.WarmupBars` (from `IObservationEncoder.
  WarmupBars`, the max across a composed brain's encoder parts -- see
  `CompositeEncoder.WarmupBars`): how many completed bars (ticks, in raw-tick mode)
  `RlEpisodeRunner.Run` consumes purely to fill `recentBars`/`recentBarHistograms`/
  `recentTicks` *before* the episode's first decision, so a windowed encoder's
  lookback (`BarWindowEncoder`'s build-time-configurable window, default 20 --
  see `EncoderSizeOptions` -- or `VolumeProfileEncoder`'s/`TimeProfileEncoder`'s
  runtime-configurable `LookbackBars`) is already full of real
  encoder's own zero-padding fallback. Warm-up bars generate no `RlStep`/reward and
  don't count toward `episodeBars`, but they DO land in `RlEpisode.Bars`/
  `BarHistograms` (and so `RlTrainingService.LatestEpisodeBars`/
  `LatestEpisodeBarHistograms`) -- visible on the Train page's "current episode"
  chart/volume/price profiles exactly like decision bars, just with no `BarInputs`/
  `BarEquity`/`BarOutputs`/trade entry possible, so the lookback window's ramp-up is
  actually watchable instead of happening off-screen. `RlEpisode.WarmupBars`/
  `RlTrainingService.LatestEpisodeWarmupBars` record the split point; `TrainLiveTab`
  draws it as a `ChartVerticalLine` (`DisplayedWarmupBoundary`, labeled "decisions
  start") on the episode replay chart, and offsets `_revealedBarCount` by it
  (`DisplayedDecisionIndex`) when indexing into the decision-only `BarInputs`/
  `BarEquity`/`BarOutputs` arrays. `RlTrainingService.TryStartTraining` reads
  `WarmupBars` off the brain right after setting `LookbackBars`, folds it into
  `fetchSpan` (so enough extra ticks are pulled up front), and passes it through to
  `RlEpisodeRunner.Run`. `Services/Training/RlEpisodeRunner.cs`,
  `Brain/Neural/Encoders/IObservationEncoder.cs`,
  `Components/Pages/Training/Pages/Train/TrainLiveTab.razor.cs`

## Core Architecture — Classes & Interfaces

- **`BrainCatalog`** — UI-facing catalog (`EncoderKind`/`ArchitectureKind` enums
  + factory methods) the Train page's Build Brain panel selects from to turn a
  picked encoder/architecture combination into real `IObservationEncoder`/
  `INetworkArchitecture` instances. See [Brain/README.md](Brain/README.md)
  ("Where brains come from") for the full picture. `Brain/Neural/BrainCatalog.cs`
- **`BarMinutesOptions`** — The doubling choice list backing `BarMinutesSelect`:
  1 minute up to 1024, plus a 1440 (1-day) cap on top since it's a meaningful
  bar size a pure power-of-two wouldn't reach. `Label(minutes)` formats each
  choice (e.g. `"64m (~1h)"`, `"1440m (1d)"`). `Helpers/BarMinutesOptions.cs`
- **`BarMinutesSelect`** — Shared "Bar minutes"/"Bar width (min)" dropdown
  component (`@bind-Value` + `Disabled`/`Label`/`Title` parameters), backed by
  `BarMinutesOptions`; used by `ProfilerPage`, `SimulationPage` (only while its
  Aggregate toggle is on), and `TrainPage`'s Training card, so all three pick
  bar size the same way instead of each repeating its own numeric input/`<select>`
  markup. `Components/Shared/BarMinutesSelect/BarMinutesSelect.razor`
- **`BrainOption`** — One entry in the brain picker: `(Name, IsNeuralNetwork)`, lets
  the UI separate trained/trainable brains from stateless heuristic ones without
  knowing about `IPolicyGradientBrain`. `Services/Training/RlTrainingService.cs`
- **`BrainRegistry`** — Shared, mutable, DI-singleton brain list that
  `RlTrainingService` and `SimulationEngine` both read live (`.All`), so a brain
  built mid-session (or loaded from a saved recipe) is immediately visible to
  both Training and Simulation without either service holding its own copy.
  `Register` adds a runtime-built brain. `Brain/BrainRegistry.cs`
- **`CategoricalSampling`** — `SampleIndex(probabilities)`: the manual cumulative-
  sum categorical sampler shared by `ComposedPolicyBrain`'s direction pick and
  every `DiscretizedLevelsActionHead`. Deliberately not TorchSharp's native
  `multinomial` -- that op has been observed to crash the whole process on this
  exact call shape (a CUDA "dtype Bool" error, then a CPU "NYI" from an internal
  `pow`), both triggered by an ordinary near-one-hot probability vector (e.g. a
  confident policy). Reading out a handful of floats and walking a cumulative sum
  in managed code sidesteps that native op entirely -- cheap and exact for the
  small (<10-way) category counts every caller has. `Brain/Neural/CategoricalSampling.cs`
- **`ComposedPolicyBrain`** — The one generic trained-brain class: pairs one
  `IObservationEncoder` (optionally a `CompositeEncoder` of several) with a
  `Module<Tensor,Tensor>` an `INetworkArchitecture` built from it, and owns the
  shared `Decide`/sampling/decision-mapping logic every composed brain uses.
  `ComposedPolicyBrain.Create` is the factory both the fixed `Program.cs`
  brains and the Train page's Build Brain panel go through; returns a
  `LookbackAwarePolicyBrain` subclass instead when at least one encoder part
  implements `IHasLookbackBars`. Full composition model (encoders +
  normalizers + architectures) documented in
  [Brain/README.md](Brain/README.md), not duplicated here.
  `Brain/Neural/ComposedPolicyBrain.cs`
- **`DummyTickGenerator`** — Static generator of synthetic even-interval random-walk
  ticks with a fixed small spread; shared by "Load Dummy Data" (Simulation) and
  headless RL episodes (Training), so both work without Postgres.
  `Services/Simulation/DummyTickGenerator.cs`
- **`IActionOutputHead`** — Sibling composable capability to `IObservationEncoder`/
  `INetworkArchitecture`, for an extra (non-direction) decision a brain's network
  can opt into: `{ Kind, OutputWidth, Sample(rawSlice) }`. A brain built with zero
  or more of these (`ActionOutputKind.Volume`/`StopLoss`/`TakeProfit`, picked via
  the Train page's Build Brain panel's Actions card) gets its network's final
  layer widened by `Σheads.OutputWidth` beyond however many directions the brain
  was built with (see `SimulationAction`'s doc comment on those also being
  individually optional, and `INetworkArchitecture.Build`'s `outputWidth`
  parameter, which is the resulting total); `Sample` decodes
  one head's slice of raw output logits into a real-unit value (capital fraction
  0..1 for `Volume`, percent-distance-from-price for `StopLoss`/`TakeProfit`; null
  = "don't set it"), that choice's log-probability (summed into the same combined
  `ActionLogProbability` the direction pick contributes to, so
  `PolicyGradientTrainingStrategy`'s REINFORCE loss needs no changes per head), and
  a labeled `RlOutputFeature` breakdown for `BrainIoPanel`. `ActionOutputEncodingKind`
  (only `DiscretizedLevels` today) is the seam for swapping in a different
  implementation later (e.g. a future continuous/Gaussian head) without touching
  `ComposedPolicyBrain`, `RlTrainingService`, or persistence -- add a new enum
  member, a new `IActionOutputHead` implementation, and a new case in
  `BrainCatalog.CreateActionOutputHead`. `DiscretizedLevelsActionHead` is the v1
  (and only) implementation: each enabled kind gets its own small independent
  categorical head (`Volume`: 25/50/75/100% of cash; `StopLoss`/`TakeProfit`:
  None/Tight/Medium/Wide, with Medium matching `RandomBrain`'s existing fixed
  0.003/0.006 defaults), sampled via `CategoricalSampling` -- the same manual
  sampler the direction pick uses. `Brain/Neural/ActionOutputs/IActionOutputHead.cs`,
  `Brain/Neural/ActionOutputs/DiscretizedLevelsActionHead.cs`,
  `Brain/Neural/BrainCatalog.cs`
- **`IBrainRepository` / `BrainRepository`** — Persists an `IPolicyGradientBrain`'s
  exported weights (TorchSharp state-dict bytes) plus the training run's
  strategy/reward-policy/parameters/metrics, and (for a runtime-built brain) the
  `BrainRecipe` needed to reconstruct its exact shape, into its own Postgres
  database (`Poink.Sim`, separate from the `Poink.MarketData` tick database). Every
  save inserts a new snapshot row (never upserts), so training history stays
  browsable. `recipe`/`trained_at` columns are nullable, additive migrations (see
  `BrainRepository.Sql.AddRecipeColumn`/`AddTrainedAtColumn`, same `ADD COLUMN IF NOT
  EXISTS` pattern as the older `dataset` column) -- a row saved before this feature
  existed just has no recipe (can't be auto-rebuilt after a restart wipes it from
  `BrainRegistry`) and an unknown trained status. `Repository/IBrainRepository.cs`
- **`IFeatureNormalizer`** — `{ Normalize(rawValue, observation), Denormalize(normalizedValue,
  anchors), Format(rawValue) }`: the half of feature engineering split out of
  `IObservationEncoder` -- an encoder decides *which* raw quantity feeds a feature
  slot, a normalizer decides *how* it's scaled into the bounded, scale-free value
  the network sees. `Normalize` is encode-time (has a live `SimulationObservation`
  to pull its anchor from); `Denormalize` is display-time, taking
  `DenormalizationAnchors` (`{ CurrentPrice, Equity }`) instead since neither
  `TrainLiveTab`'s historical replay bar nor a live Simulation decision has a full
  `SimulationObservation` on hand for display purposes -- returns `null` when its
  anchor isn't available, so the caller falls back to the plain normalized value.
  `Denormalize`/`Format` (not `Normalize`, which needs the internal
  `SimulationObservation`) are pulled out onto a public parent interface,
  `IFeatureDisplayNormalizer`, purely so `RlInputFeature.Normalizer` can be typed
  with something a Razor `[Parameter]` (`BrainIoPanel`) can legally reference --
  every `IFeatureNormalizer` implementation satisfies it automatically via that
  inheritance, no per-normalizer change needed. Both interfaces and
  `DenormalizationAnchors` live in `Brain/Neural/Normalizers/IFeatureNormalizer.cs`.
  Implementations:
  `PriceOffsetNormalizer` (percent-change-from-current-price, invert via
  `currentPrice * (1 + v)`), `EquityRatioNormalizer` (`rawAmount / equity`, invert
  via `v * equity`; available for injection, not wired into any encoder by
  default), `FractionNormalizer` (passthrough for an already-`[0,1]`
  histogram-bucket share, formats as a percentage), `DirectionNormalizer`
  (passthrough for a categorical `-1/0/1`, formats as `LONG`/`SHORT`/`FLAT`),
  `RawPriceNormalizer`/`RawValueNormalizer` (passthrough for an already-real,
  unscaled price/quantity -- `Denormalize` is the identity, no anchor needed).
  Each encoder is constructed with the normalizer(s) it needs (optional
  constructor parameters defaulting to the standard implementation), pairing
  each feature label with its normalizer via `FeatureDescriptor` (see
  `IObservationEncoder`). `Brain/Neural/Normalizers/`
- **`IHasLookbackBars`** — `{ int LookbackBars { get; set; } }`, implemented by
  `VolumeProfileEncoder`/`TimeProfileEncoder` and their raw counterparts
  `VolumeProfileRawEncoder`/`TimeProfileRawEncoder` (all default 50) -- how many
  trailing completed bars' histograms each merges into one profile each decision.
  `ComposedPolicyBrain.Create` surfaces it onto the composed brain via
  `LookbackAwarePolicyBrain` whenever one or more parts implement it, broadcasting
  one configured value to every one of them (so a brain combining multiple
  Volume/Time Profile encoders shares a single lookback window rather than
  needing separate settings); `RlTrainingService.GetLookbackBars(brainName)`
  queries it generically by name (used by `TrainLiveTab`'s "what range the
  trader sees" chart marker -- `BarWindowEncoder`'s own window has no such
  interface, since -- unlike `LookbackBars` -- it changes `FeatureCount`, so it
  can only be set once at construction (see `EncoderSizeOptions`), not adjusted
  on a live brain; that side derives its count from `DescribeInputs`' `t-N`
  labels instead). Distinct from but related to `IObservationEncoder.WarmupBars`
  (see **Warm-up bars**): `LookbackBars` is *settable* (a UI-configured merge
  width that never changes `FeatureCount`), while `WarmupBars` is each
  encoder's own read-only "how far back do I need real data" -- these
  encoders' `WarmupBars` just returns their own `LookbackBars`.
  `Brain/Neural/IHasLookbackBars.cs`
- **`IPolicyGradientBrain`** — Sibling interface (not a replacement) to
  `ISimulationBrain`, for trainable stochastic-policy networks. Adds `Network` (live
  `Module<Tensor,Tensor>`), `Encode`, `DecideWithLogProbability`, `DescribeInputs`,
  plus default-implemented `ExportWeights`/`ImportWeights`/`WeightCount`/
  `MoveToTrainingDevice`/`MoveToInferenceDevice`/`DescribeNetworkShape`.
  `DecideWithLogProbability` also returns `Outputs` (the same forward pass's full
  labeled `RlOutputFeature` distribution, not just the sampled action's log-prob) --
  free to include since `log_softmax` already computes it before sampling collapses
  it to one index (see `ComposedPolicyBrain.DescribeOutputs`).
  `Brain/Neural/IPolicyGradientBrain.cs`
- **`IRewardPolicy`** — `{ Name, OnEpisodeStart(), ComputeReward(RewardContext) }`: the
  piece that used to be a single hardcoded line inside `RlEpisodeRunner.RecordStep`
  (reward = equity drift). `ComputeReward` is a pure function of `RewardContext`
  (`PreviousCapital` -- the previous decision's whole post-Apply `CapitalState`, not
  just its `Equity`, so a policy can also read e.g. `RealizedPnl`'s own drift --
  `CapitalNow`, `PreviousPosition`, `Position`, `TimestampUtc`, plus `CurrentPrice`/
  `RecentBars`/`RecentBarHistograms` -- the same market data `RecordStep` threads
  into `SimulationObservation` one line later, empty/zero in raw-tick mode just like
  `SimulationObservation.RecentBarHistograms`); instances are long-lived DI
  singletons reused across every episode, so `ComputeReward` itself carries no
  internal state by default. `OnEpisodeStart` (default no-op) is the escape hatch
  for a policy that needs to -- called once per episode by `RlEpisodeRunner.Run`,
  before that episode's first `ComputeReward`, so a stateful implementation resets
  per-episode instead of leaking state across the singleton's whole lifetime. Same
  "pick by `Name`" house style as `IRlTrainingStrategy`
  (`RlTrainingService.RewardPolicyNames`, picked per training run via the Train
  page's Reward Policy select), but orthogonal to it: `IRewardPolicy` decides *what*
  the raw per-step reward is, `IRlTrainingStrategy`/`PolicyGradientTrainingStrategy`
  still owns turning that into discounted, mean-centered-baseline returns and the
  REINFORCE loss unchanged. Four implementations: `EquityDriftRewardPolicy` (the
  default -- raw equity drift, no shaping), `TradingRulesRewardPolicy` (same base,
  plus loss-aversion scaling on negative steps, an anti-churn cost when a new
  position opens, a growing penalty once a position's age exceeds a configured max
  holding duration, and a bonus fraction of any step's `RealizedPnl` increase --
  "realized is better than unrealized," rewarding locking in a gain over letting it
  ride at risk), `DifferentialSharpeRewardPolicy` (Moody & Saffell's Differential
  Sharpe Ratio -- rewards the step-by-step change in trailing risk-adjusted return
  instead of raw equity movement, via exponential mean/variance estimates that are
  the one example of `OnEpisodeStart`-managed state), and `MarketStructureRewardPolicy`
  (same base, plus a bonus/penalty at entry from where price sits against the
  current Volume Profile/Time Profile: favors continuing a breakout outside the
  Value Area when Bar Window momentum agrees, or fading back to the Point of
  Control inside it, scaled by how much the Volume-Profile POC and Time-Profile POC
  agree -- confluence trusted, divergence discounted toward zero).
  `Services/Training/RewardPolicies/`
- **`IRlTrainingStrategy`** — The "coach" abstraction: `Name`/`CanTrain`/
  `CreateOptimizer`/`TrainEpisode`. Kept stateless like Poink.ML's brain interfaces;
  `RlTrainingService` owns the optimizer's lifetime across a whole run so Adam's
  momentum persists correctly across episodes. `Services/Training/IRlTrainingStrategy.cs`
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
- **`LstmArchitecture`** — `INetworkArchitecture` implementation: an LSTM
  (hidden size 64, 2 layers by default) over a leading sequence slice plus
  trailing scalars, via the hand-written `LstmPolicyNetwork` (LSTM's
  tuple-returning `forward` doesn't fit `Sequential`). Not a standalone brain
  class — `"LSTM Policy"` is the name of the fixed `ComposedPolicyBrain`
  `Program.cs` builds from this + `BarWindowEncoder`/`TraderStateEncoder`; each
  `Decide()` starts the LSTM from a fresh zero hidden/cell state (no state
  carried across ticks). `Brain/Neural/Architectures/LstmArchitecture.cs`
- **MarketSim** — Code-comment name (see `Services/Simulation/SimulationEngine.cs`'s
  header) for the tick-streaming piece of Simulation, not a standalone class:
  streams historical ticks for a chosen symbol/date-range from Postgres via a
  live, forward-only `ITickRepository.ReadTicks` reader as playback advances.
  See [Docs/Simulation.md](Docs/Simulation.md)'s "What it is" for the prose
  description (there called "Tick stream").
- **`MlpArchitecture`** — `INetworkArchitecture` implementation: a flat
  `Linear→Tanh` stack over any hidden-layer sizes. Not a standalone brain
  class — `"MLP Policy"` is the name of the fixed `ComposedPolicyBrain`
  `Program.cs` builds from this + `BarWindowEncoder`/`TraderStateEncoder`
  (hidden layers 32, 16); weights persist across runs (built once, mutated in
  place by training). `Brain/Neural/Architectures/MlpArchitecture.cs`
- **`MomentumThresholdBrain`** — Heuristic, rule-based brain: opens/holds a position
  when price moves beyond `±ThresholdPercent` vs. `Lookback` observations ago, closes
  when momentum reverts toward flat (within `RevertPercent`). Stateless, deterministic.
  `Brain/Heuristic/MomentumThresholdBrain.cs`
- **`PolicyGradientTrainingStrategy`** — v1's concrete `IRlTrainingStrategy`: REINFORCE
  with discounted reward-to-go and a mean-centered baseline; uses `optim.Adam`.
  `Services/Training/PolicyGradientTrainingStrategy.cs`
- **`RandomBrain`** — Trivial baseline brain: each decision point rolls a probability
  (`ActionProbability`) to act at all, then rolls direction; the only brain with
  randomness, so it's the one where multiple traders sharing the same instance
  actually diverge. Sets `StopLossPrice`/`TakeProfitPrice` on every entry (a fixed
  percent distance from `CurrentPrice`, `_stopLossPercent`/`_takeProfitPercent`) --
  the only brain that does, useful for exercising `Trader.Advance`'s SL/TP hook and
  the chart's SL/TP lines end-to-end. `Brain/Heuristic/RandomBrain.cs`
- **`RandomEpisodeWindow`** — Picks a random start timestamp within a
  table's known range and streams the ticks spanning `fetchSpan`
  (`barWidthMinutes × (episodeBars + warmupBars + 1)` -- see **Warm-up
  bars**) from there per training episode via `ITickRepository.ReadTicks`
  (still a lazy, chunk-pruned read -- nothing here materializes more than
  one episode's window), so successive episodes train against different
  real market windows without ever holding the full range in memory;
  retries a few times on landing in an empty gap (e.g. a weekend) before
  falling back to the range start. `Services/Training/RlTrainingService.cs`
- **`RingBuffer<T>`** — Fixed-capacity circular buffer implementing
  `IReadOnlyList<T>`, most-recent-last; O(1) `Add` (vs. O(capacity) for
  `List.RemoveAt(0)`), used for the engine's bounded recent-ticks/bars/log windows.
  `Services/Simulation/RingBuffer.cs`
- **`RlEpisodeRunner`** — Static, headless single-episode playback: mirrors
  `SimulationEngine`'s tick loop (one Trader, aggregate-or-raw observation building)
  but with no Timer/locking/UI publish, so training can blast through many episodes
  fast. Data-source agnostic (takes an already-materialized tick list).
  `Services/Training/RlEpisodeRunner.cs`
- **`RlTrainingService`** — Background training orchestrator DI singleton with the
  same `_lock`/`IsRunning`/`Error`/`OnChanged` shape as `SimulationEngine` and every
  Poink.ML `*TrainingService`. Drives `RlEpisodeRunner` in a tight loop, keeps a
  bounded rolling log (`RecentEpisodes`, last 500) and restores/persists brain weights
  via `IBrainRepository`. `StepTraining` (the Train page's STEP button) reopens the
  same Pause/Resume gate for exactly one episode via `_pauseAfterNextEpisode`, so
  the loop auto-re-pauses right after that episode instead of free-running;
  `TryStartTraining`'s `pauseAfterFirstEpisode` does the same for a not-yet-started
  run, so stepping can begin from episode 1. `LatestEpisodeBars`/
  `LatestEpisodeBarHistograms`/`LatestEpisodeTrades`/`LatestEpisodeBarInputs`/
  `LatestEpisodeBarEquity`/`LatestEpisodeBarOutputs` hold only the most recently
  completed episode's full bar/histogram/closed-trade/per-decision-input/
  per-decision-equity/per-decision-output data (overwritten every episode, not
  a bounded log) -- feeds `TrainLiveTab`'s "current episode" bar chart,
  volume/price profiles, and episode replay (including its de-normalized
  inputs toggle and OUTPUTS panel). `GetLookbackBars(brainName)` looks up a
  brain's `IHasLookbackBars.LookbackBars` by name for the same panel's "what
  range the trader sees" chart marker. `Services/Training/RlTrainingService.cs`
- **`SimulationBarAggregator`** — Stateful incremental tick→HLOC/AVDT bar aggregator:
  "feed one tick, get zero-or-one completed bar back," since a sim advances
  tick-by-tick (unlike Poink.ML's batch/whole-list aggregators). Per-tick math
  mirrors Poink.ML's `HlocAvdtAggregator.Aggregate` exactly.
  `Services/Simulation/SimulationBarAggregator.cs`
- **`SimulationEngine`** — The orchestrating DI singleton: wires MarketSim (tick
  reader) + `SimulationBarAggregator` + Brain + one-or-more `Trader`s, driven by a
  self-rescheduling `System.Threading.Timer` for play/pause/step/speed/Max playback.
  `SetTraderMode`/`SubmitManualDecision` let the Simulation page's trader controls
  panel take over a session from the Brain (see `TraderControlMode`) -- both are
  ordinary public methods callable any time a run is loaded, not gated behind
  play/pause. `SetAggregation` likewise swaps the live `SimulationBarAggregator`/
  bar size (or drops to raw ticks) at any point during a loaded run, replaying the
  retained recent-ticks buffer through a fresh aggregator to rebuild bar history
  under the new size instead of blanking the chart (older history than that ticks
  window covers is simply gone). Holds one shared in-memory "current run" for the
  whole process (not per-user/session), matching Poink.ML's `*TrainingService`
  singleton convention. `Services/Simulation/SimulationEngine.cs`
- **`SystemPerformanceMonitor`** — Singleton sampling host-machine CPU/RAM/GPU once
  per second (Windows-only, via `PerformanceCounter`/`GlobalMemoryStatusEx`), feeding
  the layout's `PerformanceMonitor` display. `Diagnostics/SystemPerformanceMonitor.cs`
- **`Trader`** — Leveraged CFD account engine, implements `ITrader`.
  `Apply(decision, bid, ask, timestamp)` applies one `SimulationDecision` at a
  time (market fills, queuing a Limit/Stop pending order, or closing/cancelling)
  regardless of whether that decision came from a Brain or the trader controls
  panel -- Trader has no notion of decision source, which is what makes manual
  override possible without touching Trader itself.
  `Advance(bid, ask, timestamp)` is the unconditional per-tick maintenance pass
  (mark-to-market, SL/TP, stop-out, pending-order trigger) -- runs for every
  session regardless of `TraderControlMode`. Tracks `TradeLog`, `MaxEquity`,
  `MaxDrawdown`. One instance per trader session in a run. `Trader/Trader.cs`
- **`ITrader`** — `Trader`'s public contract, extracted so `SimulationEngine`/
  `RlEpisodeRunner` depend on the interface instead of the concrete class --
  decouples `Trader` for direct unit testing (a test can substitute a fake/mock
  `ITrader`). `Trader/ITrader.cs`
- **`ITraderFactory`** / **`TraderFactory`** — DI seam for `Trader` construction,
  used by `SimulationEngine.CreateSessions` and `RlEpisodeRunner.Run` instead of
  `new Trader.Trader(...)` directly, so an orchestration-level test can
  substitute a fake `ITraderFactory` returning a mock `ITrader` without a real
  `Trader` underneath. `TraderFactory.Create` is a one-line passthrough to
  `new Trader(startingCash, settings)`; registered as an `AddSingleton` (stateless).
  `Trader/ITraderFactory.cs`, `Trader/TraderFactory.cs`
- **`TraderSession`** — One configured trader's slice of a shared run: its own
  `Trader`/recent-log buffer/`Mode` (`TraderControlMode`), all sharing the single
  `_activeBrain` instance for the run (unless `Mode` is `Manual`, in which case
  this session ignores the Brain -- see `TraderControlMode`). Private nested
  class in `Services/Simulation/SimulationEngine.cs`.
- **`TraderSettings`** — Bundles one Trader's CFD account configuration --
  `Leverage`, `StopOutLevelPercent`, `CommissionModel` -- into a single object
  passed to `Trader`'s constructor, instead of a growing parameter list. Defaults
  reproduce pre-CFD behavior exactly (1x leverage, no commission).
  `Trader/TraderSettings.cs`

## Data Shapes

- **`BarPriceHistogram`** — Sparse per-bar, per-price-bucket tick-count breakdown
  (with up/down split), quantized to a shared `BucketWidth` so multiple bars'
  histograms can be summed bucket-for-bucket (`Merge`) to build a volume profile over
  an arbitrarily long window without retaining raw ticks. `MergeTime` is the Time
  Profile counterpart: same bucket-for-bucket sum, but each bar's tick counts are
  first weighted by that bar's average tick interval (`(CloseTime - OpenTime) /
  Volume`) to approximate seconds spent per bucket. Both `Merge` and `MergeTime` feed
  either a chart (`TickVolumeProfileChart`/`TickTimeProfileChart`) or a brain encoder
  (`VolumeProfileEncoder`/`TimeProfileEncoder` and their unnormalized counterparts
  `VolumeProfileRawEncoder`/`TimeProfileRawEncoder`) depending on the caller.
  `Models/BarPriceHistogram.cs`
- **`BrainTrainingParameters`** — Free-form training-run parameters (`EpisodeCount`,
  `EpisodeBars`, `BarWidthMinutes`, `StartingCash`, `LearningRate`, `LookbackBars`,
  `Leverage`, `StopOutLevelPercent`, `CommissionRatePercent`) saved alongside a
  brain snapshot; plain settable properties (not a record) so old JSON blobs still
  deserialize after new fields are added. `EpisodeCount` is the brain's actual
  completed-episode count in this process (`RlTrainingService._episodesTrainedByBrain`),
  not the Train page form's configured target -- saving the target made an
  untrained, just-built brain indistinguishable from a properly trained one.
  `Repository/IBrainRepository.cs`
- **`BrainRecipe`** — `{ EncoderKinds, ArchitectureKind, HiddenSizes, ActionOutputKinds,
  ActionOutputEncodingKind, EnabledDirections, EncoderSizes }`: a runtime-built
  brain's exact shape, as picked in the Train page's Build Brain panel (now
  organized as four cards -- Input/Input Structure/Structure/Actions).
  `ActionOutputKinds` (default null, treated as
  empty -- see `IActionOutputHead`) and `ActionOutputEncodingKind` (default
  `DiscretizedLevels`) round out the shape with which extra decisions the network
  makes and how; `EnabledDirections` (default null, treated as
  `ComposedPolicyBrain.AllDirections`) records which subset of `SimulationAction`
  the direction head was narrowed to -- every one of Hold/EnterLong/EnterShort/
  Close is individually optional per brain in the Actions card, not just the
  added Volume/StopLoss/TakeProfit heads. `EncoderSizes` (default null, treated
  as `EncoderSizeOptions`' own all-20 default -- see that entry) records each
  selected encoder's own input width. Populated by `RlTrainingService.TryBuildBrain`, persisted alongside
  a saved snapshot (null for the four Program.cs-fixed brains, which reconstruct via
  their own source code every restart instead), and read back by `LoadSavedBrain`
  to rebuild a runtime brain that's been wiped from `BrainRegistry` by a restart
  before importing its weights. Public (like `EncoderKind`/`ArchitectureKind`,
  elevated from `internal` for this) because it travels on `SavedBrainSummary`.
  `Brain/Neural/BrainCatalog.cs`
- **`EncoderSizeOptions`** — `{ BarWindowSize, VolumeProfileBucketCount,
  TimeProfileBucketCount, VolumeProfileRawBucketCount, TimeProfileRawBucketCount }`
  (all default 20): each pickable encoder's own input width, set independently
  in the Build Brain panel's Input Structure card and shown live there via
  `TrainPage`'s `TotalInputCount`. Unlike `IHasLookbackBars.LookbackBars` (a
  runtime-mutable merge window that never changes `FeatureCount`), these
  values *do* change `FeatureCount` -- Bar Window's window size directly *is*
  its input count (one feature per bar, no rebinning), and the profile
  encoders' bucket count directly sizes their rebinned histogram -- so they're
  build-time-only, baked into each encoder at construction via
  `BrainCatalog.CreateEncoder` and carried on `BrainRecipe` so `LoadSavedBrain`
  reconstructs the exact shape a saved snapshot's weights were trained with.
  `Brain/Neural/BrainCatalog.cs`. The Structure card's header warns (via
  `TrainPage`'s `FirstLayerTooSmall`) when the first hidden layer -- an MLP's
  Layer 1, or an LSTM's hidden size -- is narrower than `TotalInputCount`, a
  common accidental bottleneck.
- **`CapitalState`** — Read-only snapshot handed to the Brain/UI: `(Cash,
  RealizedPnl, UnrealizedPnl, MarginUsed, MarginLevel)`, with `Equity => Cash +
  UnrealizedPnl` and `MarginFree => Equity - MarginUsed`. Every field (plus
  `Equity`, `SimulationObservation.CurrentPrice`, `Position?.EntryPrice`,
  `Position?.TicksHeld`/`CumulativeDistance` (see `Position`'s doc comment), and
  whether a position is open) is fed to every neural brain via
  `TraderStateEncoder`. `Models/SimulationAccount.cs`
- **`ChartLinePoint`** — One `(TimeUtc, Price)` point of a computed overlay line (e.g.
  VWAP) on `DrawableOhlcvChart` — a per-bar time series, unlike the single fixed price
  of `ChartHorizontalLine`, so it renders as its own native LineSeries rather than one
  of the click-to-draw primitives. `Components/Charts/DrawableOhlcvChart.razor.cs`
- **`MaintenanceResult`** — Return shape of `Trader.Advance`: `(CapitalState Capital,
  IReadOnlyList<TradeExecution> AutomaticFills)`. `AutomaticFills` is 0, 1, or
  (rarely, e.g. a stop-out immediately followed by a pending-order trigger in the
  same tick) 2 entries; callers log each one the same way they log a brain-driven
  `Apply` fill. `Models/SimulationAccount.cs`
- **`NetworkLayerShape`** — One "layer" description for `NetworkEmulator`'s animation:
  a real magnitude (Linear's `out_features` or LSTM's hidden size), not a literal
  one-dot-per-unit count; `IsRecurrent` flags an LSTM so the animation can draw it
  differently. `Brain/Neural/IPolicyGradientBrain.cs`
- **`PendingOrder`** — A resting Limit/Stop entry order, not yet filled:
  `(Direction, OrderType, TriggerPrice, CapitalFraction, StopLossPrice?,
  TakeProfitPrice?, CreatedTime, Reason?)`. `OrderType` is always `Limit` or
  `Stop` here -- Market orders never rest, they fill immediately in
  `Trader.Apply`. `Trader/Orders/PendingOrder.cs`
- **`RlEpisodeWindow`** — `(EpisodeNumber, Start, End)`: one historic episode's
  actual sampled tick timestamps (see `RlEpisode`'s `WindowStart`/`WindowEnd`),
  only recorded for a run sampling a real data range (never for synthetic/dummy
  episodes). Feeds `EpisodeCoverageTimeline`'s per-block usage shading.
  `Services/Training/RlEpisode.cs`
- **`RlInputFeature`** — One named value out of a policy brain's `Encode()` input,
  purely for display (the shared `BrainIoPanel`'s INPUTS card, used by both the
  Train page and Simulation's INPUTS/OUTPUTS tab) — never used for inference
  itself. Record `(Label, Value, Normalizer)` -- `Normalizer` is the same
  normalizer that produced `Value` (see `IObservationEncoder.Features`), typed as
  `IFeatureDisplayNormalizer` (not the full `IFeatureNormalizer`, see that entry)
  so `BrainIoPanel` can invert any feature back to real units generically, without
  a per-label switch. Public (not internal like most of `Brain/Neural/`) because
  it's a `BrainIoPanel` `[Parameter]`, shared across the Train/Simulation page
  boundary. `Brain/Neural/IPolicyGradientBrain.cs`. Label
  conventions, one per encoder -- ranges below reflect each encoder's default
  size (`EncoderSizeOptions`); every N scales with whatever bucket/window count
  the brain was actually built with: `BarWindowEncoder` emits `t-N..t-1`
  (`PriceOffsetNormalizer`, default `t-20..t-1`); `VolumeProfileEncoder` emits
  `vp-0..vp-{N-1}`
  (`FractionNormalizer`, default `vp-0..vp-19`) plus `poc-offset`/`va-high-offset`/`va-low-offset`
  (`PriceOffsetNormalizer`); `TimeProfileEncoder` emits `tp-0..tp-{N-1}`
  (`FractionNormalizer`, default `tp-0..tp-19`) plus `tp-poc-offset`/`tp-va-high-offset`/
  `tp-va-low-offset` (`PriceOffsetNormalizer`; prefixed with `tp-`, unlike Volume
  Profile's bare offset labels, so the two encoders' labels never collide when
  both are part of the same composed brain); `VolumeProfileRawEncoder` emits
  `vpr-price-0..{N-1}` (`RawPriceNormalizer`, default `vpr-price-0..19`) paired with `vpr-vol-0..{N-1}`
  (`RawValueNormalizer`) -- a bucket's price isn't recoverable from its index
  alone (the rebin range shifts with the lookback window), so it's fed
  alongside the count, not instead of it -- plus `vpr-poc`/`vpr-va-high`/
  `vpr-va-low` (`RawPriceNormalizer`); `TimeProfileRawEncoder` emits
  `tpr-price-0..{N-1}`/`tpr-sec-0..{N-1}` the same way (default `..19`), plus `tpr-poc`/
  `tpr-va-high`/`tpr-va-low` (`vpr-`/`tpr-` prefixes so all four Volume/Time
  Profile encoders' labels can coexist in one composed brain);
  `TraderStateEncoder` emits `position`
  (`DirectionNormalizer`), `position-price`/`position-distance`/`current-price`
  (`RawPriceNormalizer`), `position-ticks`/`cash`/`realized`/`unrealized`/
  `equity`/`margin`/`margin-level`/`position-count` (`RawValueNormalizer`).
  The caller resolves `BrainIoPanel`'s `CurrentPrice`/`Equity` anchors before
  handing them in -- `TrainLiveTab`'s `DisplayedCurrentPrice`/`DisplayedEquity`
  stand in for a live `SimulationObservation`'s anchors (a historical replay bar,
  not a live one); `SimulationPage`'s INPUTS/OUTPUTS tab passes the live
  snapshot's `CurrentPrice`/`SelectedTrader.Capital.Equity` directly (see
  `DenormalizationAnchors`).
  `Brain/Neural/Encoders/BarWindowEncoder.cs`,
  `Brain/Neural/Encoders/VolumeProfileEncoder.cs`,
  `Brain/Neural/Encoders/TimeProfileEncoder.cs`,
  `Brain/Neural/Encoders/VolumeProfileRawEncoder.cs`,
  `Brain/Neural/Encoders/TimeProfileRawEncoder.cs`,
  `Brain/Neural/Encoders/TraderStateEncoder.cs`
- **`RlOutputFeature`** — One choice's probability out of one output head's full
  distribution for one decision, mirroring `RlInputFeature` but for what the
  network decided rather than what fed it. Record `(Label, Probability, IsChosen,
  Group = "Direction")` -- probabilities *within one `Group`* sum to ~1, not
  across the whole list: a brain always has one `"Direction"` group (`Label` the
  `SimulationAction` member name "Hold"/"EnterLong"/"EnterShort"/"Close" -- one
  row per direction the brain was built with, not always 4, see that enum's doc
  comment) plus one group per enabled `IActionOutputHead` (`"Volume"`/`"StopLoss"`/
  `"TakeProfit"`, `Label` the sampled head's level name, e.g. "Tight"). `IsChosen`
  flags whichever choice the stochastic sampler actually picked within its group
  (not necessarily the highest-probability one). Produced by
  `ComposedPolicyBrain.DecideWithLogProbability` from the same forward pass that
  decides, not a second one -- `SimulationEngine.DecideAndApply` calls this
  instead of the plain `Decide` for whichever one trader session is currently
  selected for I/O (`SelectTraderForIo`/`TraderSnapshot.Outputs`), since
  `ComposedPolicyBrain.Decide` already runs this same forward pass internally
  and discards the distribution, so capturing it there costs nothing extra.
  `BrainIoPanel`'s OUTPUTS card groups `Outputs` by `Group` and renders each as
  its own `action-dist-*`-styled bar-set (the `"Direction"` group keeps its
  existing HOLD/LONG/SHORT/CLOSE coloring via `ActionShortLabel`/`ActionCssSuffix`;
  other groups render their level label as-is with a neutral `action-dist-level`
  style) -- the same family `TrainLiveTab`'s separate, session-wide ACTION
  DISTRIBUTION panel uses, duplicated rather than shared across the Blazor
  CSS-isolation boundary, with `.action-dist-row-chosen` highlighting `IsChosen`.
  Public for the same `BrainIoPanel` reason as `RlInputFeature`.
  `Brain/Neural/IPolicyGradientBrain.cs`
- **`SavedBrainSummary`** — Metadata-only view of one saved brain snapshot (everything
  except the weight bytes themselves), used to populate `SavedBrainsTable` without
  pulling megabytes of weights per row. `RewardPolicyName` records which
  `IRewardPolicy` shaped this brain's training, same treatment as `StrategyName`.
  `Recipe` (nullable `BrainRecipe`) and `TrainedAt` (nullable, distinct from
  `SavedAt`) are null for a snapshot saved before this feature existed, or (for
  `TrainedAt`) one saved before any training episode actually completed --
  `SavedBrainsTable` renders that as a "Structure"/"Untrained" row rather than
  assuming every saved brain was genuinely trained. `Repository/IBrainRepository.cs`
- **`SimulationProgress`** — Cheap sibling of `SimulationSnapshot`: just
  cursor/total/timestamp/range, published far more often during an uncapped Max run
  so a lightweight component (`SimTimeline`) can track live progress without paying
  for the full snapshot's copies. `Models/SimulationSnapshot.cs`
- **`SimulationSnapshot`** — Immutable read-model published wholesale by
  `SimulationEngine` on every callback (config, playback state, per-trader state,
  recent bars/ticks/histograms) — what the page renders from.
  `Models/SimulationSnapshot.cs`
- **`TimedPrice`** — `(Timestamp, Price)`: the minimum `TickTimeProfileChart` needs
  from a raw `Tick` to measure dwell time between consecutive ticks in raw-tick mode;
  a public stand-in since `Tick` itself is internal.
  `Components/Charts/TickTimeProfileChart/TickTimeProfileChart.razor.cs`
- **`TimeProfileLevels`** — `(Poc, ValueAreaHigh, ValueAreaLow)`: the computed
  Point of Control/Value Area for a Time Profile window, from
  `TimeProfileStats.Compute`. `Components/Charts/TimeProfileStats.cs`
- **`TraderSnapshot`** — One trader session's published state: `Mode`
  (`TraderControlMode`), `Capital`, `Position`, `PendingOrder`, trade log/history,
  equity extremes, account settings. What `SimulationPage`'s trader controls
  panel reads to decide which controls to show (see `TraderControlMode`).
  `Inputs`/`Outputs` (nullable `RlInputFeature`/`RlOutputFeature` lists) feed the
  INPUTS/OUTPUTS tab's `BrainIoPanel` -- populated only for whichever one session
  `SimulationEngine.SelectTraderForIo` currently designates (capturing for every
  session on every decision would multiply the extra
  `DescribeInputs`/`DecideWithLogProbability` cost by `TraderCount` for no
  benefit, since only one trader's I/O is ever shown at a time), null for a
  Manual-mode session or a non-`IPolicyGradientBrain` (heuristic) brain.
  `Models/SimulationSnapshot.cs`
- **`VolumeProfileLevels`** — `(Poc, ValueAreaHigh, ValueAreaLow)`: the computed
  Point of Control/Value Area for a volume profile window, from
  `VolumeProfileStats.Compute`. `Components/Charts/VolumeProfileStats.cs`
- **`WeightedPriceSample`** — One price level's contribution to the volume profile:
  `(Price, Count, UpCount, DownCount)`, either a single raw tick (Count=1) or a merged
  multi-tick bucket total.
- **`WeightedTimeSample`** — Time Profile's counterpart to `WeightedPriceSample`:
  `(Price, Count, Seconds, UpSeconds, DownSeconds)`. `Count` still rides along purely
  to gate `TickTimeProfileChart`'s "enough data yet" floor on sample count,
  independent of how much wall-clock time those samples span.
  `Components/Charts/TickTimeProfileChart/TickTimeProfileChart.razor.cs`

## Enums & States

- **`CalendarView`** — `Days`, `Months`, `Years`: the three drill-down levels of the
  date picker. Private enum in `Components/Shared/Calendar/Calendar.razor.cs`.
- **`LogTab`** — `Decisions`, `Positions`, `Traders`, `Brain`, `InputsOutputs`,
  `Statistics`: the six tabs of the Simulation page's log/detail panel (rendered
  as TRADERS, BRAIN, INPUTS/OUTPUTS, DECISION LOG, POSITIONS, STATISTICS).
  `InputsOutputs` renders `BrainIoPanel` for `SelectedTrader`. Private enum in
  `Components/Pages/Simulation/SimulationPage.razor.cs`.
- **`OrderType`** — `Market`, `Limit`, `Stop`: how `SimulationDecision.Action`'s
  `EnterLong`/`EnterShort` fills. `null`/`Market` (the default) opens immediately,
  as every brain does today; `Limit`/`Stop` with a `TriggerPrice` queues a
  `PendingOrder` instead. Deliberately not a new `SimulationAction` member -- it's
  a property of *how* to enter, not a distinct direction choice, same reasoning as
  `CapitalFraction`/`StopLossPrice`/`TakeProfitPrice` (see `IActionOutputHead`, the
  actual extension point for growing what a brain can decide).
  `Models/SimulationDecision.cs`
- **`PositionDirection`** — `Long`, `Short`: direction of an open position; determines
  the sign in `UnrealizedPnl`. `Models/SimulationAccount.cs`
- **Speed presets** — `0.1x, 1x, 10x, 100x, 1000x, 10000x`: the finite playback-speed
  multipliers the +/- stepper cycles through, separate from the uncapped "Max" mode.
  `Components/Pages/Simulation/SimulationPage.razor.cs` (`Speeds` array)
- **`SimulationAction`** — `Hold`, `EnterLong`, `EnterShort`, `Close`: the fixed set
  of *direction* choices a Brain can output -- never grown or replaced (that's what
  `IActionOutputHead` is for, appending logits for extra non-direction decisions
  like Volume/StopLoss/TakeProfit after the direction ones). Public (elevated from
  internal) because `BrainRecipe.EnabledDirections` needs to reference it. Every
  member is individually optional per brain, not just the added heads: the Train
  page's Build Brain panel's Actions card has a checkbox per direction (all 4
  checked by default), `RlTrainingService.TryBuildBrain` threads the picked subset
  through as `enabledDirections`, and `ComposedPolicyBrain.Create` sizes the
  direction head to exactly that many logits (`ComposedPolicyBrain.AllDirections`
  is the canonical order + all-4 default for a brain that doesn't specify a
  subset, e.g. Program.cs's fixed brains) -- `BuildDecision` switches on the
  sampled `SimulationAction` value itself (not a raw index) since a brain missing
  e.g. `Hold` no longer has index 0 mean `Hold`. `Close` does double duty: closes
  the open position if one exists, else cancels a resting `PendingOrder`, else
  no-op. `Models/SimulationDecision.cs`, `Brain/Neural/ComposedPolicyBrain.cs`
- **`StatEmphasis`** — `None`, `Positive`, `Negative`: display hint for a `StatItem`,
  mapped by `StatisticsPanel` to CSS classes (win/loss coloring).
  `Models/StatGroup.cs`
- **`TraderControlMode`** — `Auto`, `Manual`: per-`TraderSession` toggle. `Auto`
  (the default) is today's behavior -- the shared Brain decides for this session
  every decision point. `Manual` skips the Brain for that session entirely
  (`SimulationEngine.DecideAndApply`); it only moves via
  `SimulationEngine.SubmitManualDecision`, called from the Simulation page's
  trader controls panel. Doesn't affect `Trader.Advance` (mark-to-market/SL-TP/
  stop-out/pending-trigger), which runs unconditionally either way -- Mode only
  gates who submits *new* entries. Toggled via `SimulationEngine.SetTraderMode`.
  `Models/SimulationSnapshot.cs`

## UI Components

- **`BrainInfoCard`** — Shows "what's actually running" for a live brain (name,
  NN-or-not, and its `ActiveSnapshot` if the live weights match a saved one) on
  `SimulationPage`'s BRAIN tab. `Components/Pages/Training/Components/BrainInfoCard/`
- **`BrainIoPanel`** — "What the network sees/decided" for one decision (INPUTS
  section: feature values with a de-normalize toggle, grouped into one shaded
  horizontal-strip card per encoder (label on the left, values flowing in a
  row to the right) -- PRICE WINDOW, VOLUME PROFILE, TIME PROFILE, TRADER
  STATE -- split purely by each `RlInputFeature.Label`'s naming convention
  (`t-N`; `vp-`/`vpr-` plus the normalized variant's unprefixed POC/value-area
  scalars; `tp-`/`tpr-`; everything else). The Volume Profile/Time Profile
  cards further shade their POC/value-area-and-(raw-only)-price features apart
  from their histogram fraction/tick-count/seconds features (PRICE vs
  VOLUME/TIME), rendered as two side-by-side sub-groups within the strip;
  OUTPUTS card: the Hold/Long/Short/Close probability distribution, sampled
  action highlighted) -- each card's title carries a pill badge with that
  card's feature/action count (`Inputs`/`Outputs`' `.Count`). `[Parameter]`s
  `Inputs`/`Outputs`/`CurrentPrice`/`Equity`
  (`RlInputFeature`/`RlOutputFeature` lists, primitive `double?` anchors). Shared
  between `TrainLiveTab` (Train page's Live tab -- `DisplayedInputs`/
  `DisplayedOutputs`, whichever decision the live episode or a frozen replay is
  currently on) and `SimulationPage`'s INPUTS/OUTPUTS tab (`SelectedTrader?.Inputs`/
  `Outputs`, the currently-selected trader's last live decision) -- callers resolve
  their own notion of "which decision" down to these four parameters; the
  component itself knows nothing about episodes, replays, or trader sessions.
  `Components/Shared/BrainIoPanel/`
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
- **`DrawableOhlcvChart`** — Candlestick + volume chart backed by the vendored
  TradingView Lightweight Charts JS library; shared by `ProfilerPage`'s main chart,
  `TrainLiveTab`'s episode replay, and `SimulationPage`'s live chart. Two independent
  groups of parameters cover each: click-to-draw annotations (H/V/D lines, T-Range/
  P-Range/Rectangle, VWAP overlay) plus lazy pan-triggered backfill for Profiler's
  needs, and live Bid/Ask price lines, the selected trader's open position as a solid
  LONG/SHORT entry-price line plus dashed SL/TP price lines (each null independently
  hides that one line -- flat, or a position missing that field), and a "window start"
  marker showing how far back the volume-profile window reaches for Simulation's.
  `TrainLiveTab` reuses it as-is for its generic `DiagonalLines` parameter
  (closed-trade entry-to-exit markers) -- its drawing toolbar/minimap render there too
  even though nothing wires their callbacks, a deliberate reuse-over-building-a-
  second-chart tradeoff. `Components/Charts/DrawableOhlcvChart/`
- **`EpisodeCoverageTimeline`** — Data-coverage heatmap at the top of `TrainPage`'s
  `train-content`: one block per `bars/episode`-length slice of the selected Data
  Range, shaded darker the more completed episodes (`RlTrainingService.
  RecentEpisodeWindows`) sampled from it. Binds to the page's live From/To/bars-
  per-episode fields, so the block layout previews before Start Training is
  clicked. `Components/Pages/Training/Components/EpisodeCoverageTimeline/`
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
- **`TickTimeProfileChart`** — Horizontal-bar time histogram (price on the row axis,
  seconds spent at that price as bar width), rendered next to `TickVolumeProfileChart`
  on the Simulation page behind its own draggable divider. Same pure Razor/CSS
  approach and up/down-split/POC/Value-Area readout as `TickVolumeProfileChart`, just
  weighted by dwell time instead of tick count. Consumes either raw `Ticks` (tick
  mode, `TimedPrice` timestamp+price pairs) or pre-aggregated `WeightedSamples` (bar
  mode, via `BarPriceHistogram.MergeTime`). This *is* the app's "Time Profile"
  concept. `Components/Charts/TickTimeProfileChart/TickTimeProfileChart.razor.cs`
- **`TickVolumeProfileChart`** — Horizontal-bar tick histogram (price on the row
  axis, tick count as bar width), rendered as pure Razor/CSS (no JS, since it's not
  pixel-synced to `DrawableOhlcvChart`'s zoom/pan). Consumes either raw `Prices` (tick mode)
  or pre-aggregated `WeightedSamples` (bar mode, via `BarPriceHistogram.Merge`). This
  *is* the app's "Volume Profile" concept.
  `Components/Charts/TickVolumeProfileChart/TickVolumeProfileChart.razor.cs`
- **`TraderControlsPanel`** — Manual trader controls, the fixed-width panel to
  the right of the chart row on the Simulation page. Shows an AUTO/MANUAL toggle
  (`ToggleManualMode`, calling `SimulationEngine.SetTraderMode`); once Manual,
  exposes a trading ticket (order type, trigger price for Limit/Stop, capital
  fraction, optional SL/TP, Buy/Sell, Close/Cancel) that submits via
  `SubmitEntry`/`SubmitCloseOrCancel` → `SimulationEngine.SubmitManualDecision`.
  Takes plain primitives (`TraderId`/`TraderLabel`/`IsManual`/`PositionSummary`/
  `PendingSummary`) rather than `TraderSnapshot` directly -- `TraderSnapshot`
  is internal like most of Poink.Sim's domain model, and this component (like
  every other one in this project) has to stay `public partial class` for the
  Razor-generated half to compile, so `SimulationPage` pre-formats/projects the
  fields instead of the domain model needing to go public just to cross this
  boundary. Its own `.razor.css` duplicates the terminal-button look from
  `SimulationPage.razor.css` (`.sim-ctrl-btn` etc.) rather than relying on it,
  since Blazor CSS isolation only cascades a parent's scoped styles onto a
  child component's own root element, not elements nested below it.
  `Components/Pages/Simulation/Components/TraderControlsPanel/`
- **`TrainLiveTab`** — The Train page's "Live" content: a full-width `EpisodeGrid`
  panel (directly under `TrainPage`'s `EpisodeCoverageTimeline`), stat tiles, a
  PAUSE/RESUME LIVE VIEW toggle, `RewardChart`, three `TrendLineChart` panels
  (median reward, reward std dev, win rate), action-distribution bars, a
  "current episode" panel, an INPUTS panel, and an OUTPUTS panel.
  `Components/Pages/Training/Pages/Train/TrainLiveTab.razor.cs`

  The **live feedback toggle** (`ToggleLiveFeedback`/`_liveFeedbackPaused`) freezes
  everything from the REWARD chart down (the three trend charts, action distribution,
  current episode panel, INPUTS/OUTPUTS, and the EPISODE/TRADER/SESSION stats tables)
  at a `LiveFeedbackSnapshot` taken the moment it's toggled on, so a run can be read
  or compared without the page redrawing every episode. Background training keeps
  running regardless -- `EpisodeGrid` and the stat tiles above the toggle (and
  `TrainPage`'s `EpisodeCoverageTimeline`) always read straight from `RlTrainingService`
  and stay live either way. Every property that used to read `Service.X` directly for
  the frozen section now reads a matching `Source*` property instead, which falls back
  to the snapshot while paused and to `Service` otherwise -- including `PlayEpisode`'s
  fresh-start snapshot, so starting a replay while paused plays back the exact frozen
  episode rather than mismatching frozen bars against a newer live episode's inputs/outputs.

  The **current episode panel** renders the most recently completed episode's
  bars via `DrawableOhlcvChart`, plus `TickVolumeProfileChart`/
  `TickTimeProfileChart` merged from `RlTrainingService.LatestEpisodeBars`/
  `LatestEpisodeBarHistograms` — reads well paired with the STEP control, which
  pauses on exactly one episode at a time. Its PLAY/PAUSE + speed stepper drive
  an **episode replay**: a local `System.Threading.Timer` reveals a frozen
  snapshot of that episode's bars one at a time (`BaseBarsPerSecond` scaled by
  the speed stepper, not real elapsed/bar-width time), with each closed trade
  (`LatestEpisodeTrades`) drawn as a `DrawableOhlcvChart` diagonal line once its
  exit bar is revealed, colored by realized P&L sign via the `win`/`loss` groups
  added to `drawableOhlcvChart.js`'s `groupColors`. The snapshot is frozen at
  Play so the still-completing episode that triggered it can't repaint
  mid-animation, but `HandleServiceChanged` drops it (`StopAndResetReplay`) the
  moment training is told to carry on again (Start/Resume/Step), so the panel
  doesn't linger on a stale episode instead of following the next one live. The
  chart also shades a `DisplayedWindowRange` (a `ChartRangeFill`, reusing
  `DrawableOhlcvChart`'s `profile` color group) over the trailing bars feeding
  the current decision — "what range the trader sees" — sized from whichever of
  `PriceWindowFeatures`' count or `RlTrainingService.GetLookbackBars` is larger.

  The **INPUTS panel** (`PriceWindowFeatures`/`ScalarFeatures` split from
  `RlInputFeature.Label`) is synced to the same revealed bar as the chart during
  a replay (`DisplayedInputs`) rather than always showing the episode's last
  decision, with a de-normalized toggle (`FormatInputValue`) that inverts each
  labeled value back to real price/currency units using whichever bar's
  close/pre-decision equity is currently shown (`DisplayedCurrentPrice`/
  `DisplayedEquity`) — see `RlInputFeature`'s doc comment for the label-by-label
  formulas.

  The **OUTPUTS panel** (`DisplayedOutputs`, synced to the same revealed bar the
  same way) is `BrainIoPanel`'s shared OUTPUTS card, showing what the network
  decided from those inputs: one `action-dist-row` (reusing ACTION DISTRIBUTION's
  styling) per `RlOutputFeature`, grouped by `Group` -- the `SimulationAction`
  direction pick plus one bar-set per `IActionOutputHead` the brain was built
  with -- with `.action-dist-row-chosen` highlighting whichever one
  `RlOutputFeature.IsChosen` flags in each group.
- **`TrendLineChart`** — Generic rolling-window trend line, reused by `TrainLiveTab`
  for the MEDIAN REWARD, REWARD STD DEV, and WIN RATE panels alongside `RewardChart`.
  Takes an `Aggregate` delegate (`Func<IReadOnlyList<RlEpisodeMetric>, double>`) so it
  owns only the windowing/SVG rendering; each caller supplies the statistic. The
  window is a widening-then-sliding trailing count of episodes (own selector, default
  20) -- widening first so the chart has something to show from episode 1 instead of
  staying empty until history fills. Only draws its zero-baseline reference when the
  plotted range actually straddles zero (median reward can go negative; std dev/win
  rate never do).
  `Components/Pages/Training/Components/TrendLineChart/`

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
- **Auto Name convention** — `{Architecture}_{Layers}_{Data}`, e.g.
  `MLP_L32-L16_dBW-dVP-dTP-dAS`: the Build Brain section's auto-generated name.
  Layers are `L<size>` per MLP hidden layer or `H<hiddenSize>-N<numLayers>` for
  LSTM; Data is `d` + each included encoder's initials (`BW`=Bar Window,
  `VP`=Volume Profile, `TP`=Time Profile, `VPR`=Volume Profile Raw,
  `TPR`=Time Profile Raw, `AS`=Trader State, always present). Toggled off via
  the "Auto Name" checkbox to type a custom name instead.
  `Components/Pages/Training/Pages/TrainPage.razor.cs` (`AutoBuildBrainName`)
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
- **LSTM** — Long Short-Term Memory (recurrent network); `LstmArchitecture`'s
  network shape, used by the `"LSTM Policy"` brain.
- **MLP** — Multi-Layer Perceptron; `MlpArchitecture`'s network shape, used by
  the `"MLP Policy"`, `"Volume Profile Policy"`, and `"Volume Profile + Bars
  Policy"` fixed brains (see `Program.cs`).
- **NN** — Neural Network (e.g. `IsNeuralNetwork` on `BrainOption`).
- **P&L** — Profit and Loss (realized/unrealized).
- **REINFORCE** — The specific policy-gradient RL algorithm (Monte Carlo policy
  gradient) implemented in `PolicyGradientTrainingStrategy`.
- **RL** — Reinforcement Learning.
- **SL/TP** — Stop-Loss / Take-Profit; marker comment (`// SL/TP CHECK`) in
  `Trader.Apply` for the not-yet-implemented auto-close hook.
- **WDDM** — Windows Display Driver Model; the GPU driver model needed for the GPU
  performance-counter categories `SystemPerformanceMonitor` reads.
