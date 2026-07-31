# Brain — pluggable trading-decision brains

> Doc version: **v1.5** — see "Changelog" at the bottom for what changed.
> See [../Docs/README.md](../Docs/README.md) for documentation standards.

This documents the **current, as-built state** of this folder — it is
authoritative over the `Brain/`-related entries in `Poink.Sim/GLOSSARY.md`,
which still describe the pre-composition brains (`MlpPolicyBrain`,
`LstmPolicyBrain`, `VolumeProfilePolicyBrain`, `VolumeProfileBarsPolicyBrain`)
that no longer exist as standalone classes — that file hasn't been updated
yet.

## TLDR

Every brain — hand-written rulebook or trained neural network — implements one
interface, `ISimulationBrain`. Two brains here are plain heuristics with no
weights. The rest are **composed at construction time from independent
pieces** — an encoder (turns a `SimulationObservation` into a feature vector)
plus a network architecture (turns that feature vector into action logits) —
instead of one hardwired class per (encoding, network-shape) combination. New
combinations can be assembled without writing a new class: either a one-line
DI registration in `Program.cs`, or at runtime through the Train page's
"Build Brain" panel.

## Two families

| Family | Interface | Examples | Has weights? |
|---|---|---|---|
| Heuristic | `ISimulationBrain` directly | `MomentumThresholdBrain`, `RandomBrain` | No — plain rules, `Reset()` is a no-op |
| Trained (policy-gradient) | `IPolicyGradientBrain` (`Neural/`) — a sibling to `ISimulationBrain`, not a replacement | every brain built by `ComposedPolicyBrain.Create` | Yes — a live TorchSharp `Module<Tensor,Tensor>`, mutated in place by training |

`ISimulationBrain` stays minimal (`Name`/`Decide`/`Reset`) on purpose — a
heuristic brain has nothing else to carry, and that's exactly what lets a
trained brain implement the same contract by holding its network privately.

## The composition model (`Neural/`)

A brain's "shape" is two independent choices, wired together by
`ComposedPolicyBrain.Create`:

| Piece | Interface | Current implementations |
|---|---|---|
| **Encoder** — observation → feature vector | `IObservationEncoder` (`FeatureCount`, `SequenceLength`, `Features`, `Encode`) | `BarWindowEncoder` (20-bar price window), `VolumeProfileEncoder` (20-bucket volume histogram + POC/value-area summary), `TimeProfileEncoder` (20-bucket time-weighted histogram + POC/value-area summary), `SessionClockEncoder` (time-of-day + day-of-week as sin/cos pairs, no windowing), `SpreadVolatilityEncoder` (current Bid/Ask spread + short-window Average True Range), `TraderStateEncoder` (12 exact trader/account fields). The first three each take a `NormalizationKind` (`Normalized`/`Raw`) picking which normalizer pair they're built with, plus (for the two profiles) their own fixed `lookbackBars` (default 50); `SpreadVolatilityEncoder` instead takes its own fixed `atrPeriod` (default 14) |
| **Normalizer** — raw quantity → network-ready value (and back, for display) | `IFeatureNormalizer` (`Normalize`, `Denormalize`, `Format`) | `PriceOffsetNormalizer` (percent-change-from-current-price), `EquityRatioNormalizer` (ratio-over-equity, available for injection), `FractionNormalizer` (passthrough for an already-`[0,1]` histogram share), `DirectionNormalizer` (passthrough for a categorical `-1/0/1`), `RawPriceNormalizer`/`RawValueNormalizer` (passthrough for an already-real price/quantity) |
| **Architecture** — feature vector → action logits | `INetworkArchitecture` (`Build(featureCount, sequenceLength)`) | `MlpArchitecture` (flat `Linear→Tanh` stack, any hidden-layer sizes), `LstmArchitecture` (LSTM over a leading sequence slice + trailing scalars, via `LstmPolicyNetwork`), `CnnArchitecture` (Conv1d stack over a leading sequence slice + trailing scalars, via `CnnPolicyNetwork`) |
| **Combine multiple encoders** | `CompositeEncoder` | Concatenates parts in order; enforces that any sequence-bearing part (`SequenceLength > 0`) is listed **first**, so a recurrent architecture narrow()s the right slice |
| **The brain itself** | `ComposedPolicyBrain` | Holds one `IObservationEncoder` + the `Module<Tensor,Tensor>` an architecture built from it; owns the one shared `Decide`/sampling/decision-mapping logic every composed brain uses |

An encoder decides *which* raw quantity feeds a given feature slot; a
normalizer decides *how* that quantity is scaled into the bounded, scale-free
value the network sees. Each encoder is constructed with the normalizer(s) it
needs (optional constructor parameters defaulting to the standard
implementation, e.g. `new BarWindowEncoder()` still works), pairing each
feature's label with its normalizer via `FeatureDescriptor`
(`IObservationEncoder.Features`). This is what lets `ComposedPolicyBrain.
DescribeInputs`/`RlInputFeature` and `TrainLiveTab`'s de-normalized INPUTS
view work generically off any encoder's features — a new encoder or a new
normalizer never needs a matching new case on the display side.

`ComposedPolicyBrain.Create(name, architecture, params encoders)` is the entry
point for a direction-only brain:

```csharp
ComposedPolicyBrain.Create(
    "Volume Profile + Bars Policy", new MlpArchitecture(64, 32, 16),
    new BarWindowEncoder(), new VolumeProfileEncoder(), new TraderStateEncoder());
```

An overload taking an `IReadOnlyList<IActionOutputHead> actionOutputHeads`
before the encoder params builds a brain whose network also decides
Volume/StopLoss/TakeProfit (see `ActionOutputs/IActionOutputHead.cs` and
`GLOSSARY.md`'s `IActionOutputHead` entry) — the Train page's Build Brain
panel is what actually constructs these via `RlTrainingService.TryBuildBrain`.

`VolumeProfileEncoder`/`TimeProfileEncoder` each merge their own fixed,
constructor-supplied `lookbackBars` (default 50) of trailing bars into one
profile per decision — independent of each other and of every other build
option, same treatment `bucketCount` always had. A brain combining both no
longer shares one lookback window between them (that was a deliberate design
choice, since replaced): each profile's window is just another per-set build
option set in the Train page's Build Brain panel (see `EncoderSizeOptions`).

## Where brains come from


**Built at runtime** — the Train page's "+ BUILD BRAIN" panel
(`Components/Pages/Training/Pages/TrainPage.razor`) lets a user pick any
encoder combination and architecture, then calls
`RlTrainingService.TryBuildBrain`, which resolves the pick through
`BrainCatalog` (the enum-driven `EncoderKind`/`ArchitectureKind` factory) and
registers the result into `BrainRegistry`. `TraderStateEncoder` is always
appended automatically — it isn't a pickable `EncoderKind`, since every brain
needs account state regardless of how market data is represented.

Both paths converge on `BrainRegistry` — a shared, mutable, DI-singleton list
that `RlTrainingService` and `SimulationEngine` both read live (`.All`), so a
brain built mid-session is immediately visible to both training and
simulation without either service needing its own copy. Runtime-built brains
are **ephemeral**: their *shape* doesn't survive an app restart (same as
every brain's shape being fixed in code today) — only trained *weights*
persist, via the existing Save Model flow, keyed by the brain's `Name`.

## Files

| File | Role |
|---|---|
| `ISimulationBrain.cs` | The brain contract every brain implements: `Name`, `Decide`, `Reset` |
| `BrainRegistry.cs` | Shared mutable brain list backing both `RlTrainingService` and `SimulationEngine`; `Register` adds a runtime-built brain |
| `Heuristic/MomentumThresholdBrain.cs` | Heuristic: opens/holds on a ±threshold price move vs. N observations ago, closes on revert |
| `Heuristic/RandomBrain.cs` | Heuristic: independently rolls whether to act and which direction each decision point — a trivial baseline |
| `Neural/IPolicyGradientBrain.cs` | Trained-brain family contract: `Network`, `Encode`, `DecideWithLogProbability`, `DescribeInputs`; default-implements `ExportWeights`/`ImportWeights`/`WeightCount`/`MoveToTrainingDevice`/`MoveToInferenceDevice`/`DescribeNetworkShape` off `Network` alone |
| `Neural/CategoricalSampling.cs` | Manual cumulative-sum categorical sampler shared by every direction/action-head pick (sidesteps a TorchSharp `multinomial` crash on this call shape) |
| `Neural/ComposedPolicyBrain.cs` | The one generic brain — pairs an encoder with a network built by an architecture; owns the shared `Decide`/sampling/decision-mapping logic every composed brain uses |
| `Neural/BrainCatalog.cs` | UI-facing catalog (`EncoderKind`/`ArchitectureKind`/`NormalizationKind` enums + factory) the Train page's Brain Builder panel selects from |
| `Neural/Encoders/IObservationEncoder.cs` | Encoder contract: `FeatureCount`, `SequenceLength`, `Features`, `Encode`; also defines `FeatureDescriptor` (`Label` + `IFeatureNormalizer`) |
| `Neural/Encoders/BarWindowEncoder.cs` | Encoder: 20-bar price window; `NormalizationKind` picks percent-change-from-current-price (Normalized) or the literal price (Raw) |
| `Neural/Encoders/TraderStateEncoder.cs` | Encoder: 12 exact trader/account fields (position direction, position entry price, position ticks held, position cumulative distance, current price, cash, realized/unrealized PnL, equity, margin, margin level, position count) |
| `Neural/Encoders/VolumeProfileEncoder.cs` | Encoder: volume histogram (default 20 buckets) + POC/value-area summary, merged from its own fixed `lookbackBars` (default 50) trailing bars; `NormalizationKind` picks fraction-of-total/percent-offset (Normalized) or exact tick counts/prices (Raw) |
| `Neural/Encoders/TimeProfileEncoder.cs` | Encoder: time-weighted (seconds-spent) histogram counterpart to `VolumeProfileEncoder`, same `NormalizationKind`/`lookbackBars` shape; labels prefixed `tp-`/`tpr-` so they don't collide with `VolumeProfileEncoder`'s when both are composed together |
| `Neural/Encoders/SessionClockEncoder.cs` | Encoder: time-of-day + day-of-week as sin/cos pairs (4 values), derived straight from `SimulationObservation.TimestampUtc` -- no windowing, no `NormalizationKind` (already bounded, nothing to scale) |
| `Neural/Encoders/SpreadVolatilityEncoder.cs` | Encoder: current Bid/Ask spread + a short trailing Average True Range (2 values); ATR needs `atrPeriod + 1` bars (default period 14), bar-only like the two profile encoders |
| `Neural/Encoders/CompositeEncoder.cs` | Concatenates multiple encoders into one, enforcing the sequence-first ordering constraint |
| `Neural/Normalizers/IFeatureNormalizer.cs` | Normalizer contract: `Normalize`, `Denormalize`, `Format`; also defines `DenormalizationAnchors` (`CurrentPrice`/`Equity`) |
| `Neural/Normalizers/PriceOffsetNormalizer.cs` | Normalizer: price ↔ percent-change-from-current-price |
| `Neural/Normalizers/EquityRatioNormalizer.cs` | Normalizer: raw amount ↔ ratio-over-equity |
| `Neural/Normalizers/FractionNormalizer.cs` | Normalizer: passthrough for an already-`[0,1]` histogram-bucket share |
| `Neural/Normalizers/DirectionNormalizer.cs` | Normalizer: passthrough for a categorical `-1/0/1` position flag |
| `Neural/Normalizers/RawPriceNormalizer.cs` | Normalizer: passthrough for an already-real, unscaled price level |
| `Neural/Normalizers/RawValueNormalizer.cs` | Normalizer: passthrough for an already-real, unscaled quantity |
| `Neural/Architectures/INetworkArchitecture.cs` | Architecture contract: `Build(featureCount, sequenceLength)` |
| `Neural/Architectures/MlpArchitecture.cs` | Architecture: flat `Linear→Tanh` stack, any hidden-layer sizes |
| `Neural/Architectures/LstmArchitecture.cs` | Architecture: LSTM over a leading sequence slice + trailing scalars |
| `Neural/Architectures/LstmPolicyNetwork.cs` | The hand-written `Module<Tensor,Tensor>` `LstmArchitecture` builds (LSTM's tuple-returning `forward` doesn't fit `Sequential`) |
| `Neural/Architectures/CnnArchitecture.cs` | Architecture: Conv1d stack over a leading sequence slice + trailing scalars, any kernel size/per-layer channel counts |
| `Neural/Architectures/CnnPolicyNetwork.cs` | The hand-written `Module<Tensor,Tensor>` `CnnArchitecture` builds (a `Sequential` Conv1d+Tanh stack, then last-timestep + mean-over-time pooling feeds a small MLP head) |
| `Neural/ActionOutputs/IActionOutputHead.cs` | Optional-decision contract (`Kind`, `OutputWidth`, `Sample`) for a non-direction network output (Volume/StopLoss/TakeProfit) |
| `Neural/ActionOutputs/DiscretizedLevelsActionHead.cs` | The v1 (and only) `IActionOutputHead` implementation: a small independent categorical head per enabled kind |

## Explicitly not (yet) in this model

- **Training policy** (`IRlTrainingStrategy`, `Services/Training/`) is already a
  separate, swappable axis — it operates on any `IPolicyGradientBrain`
  generically. Not part of this folder, not touched by the composition work
  above.
- **Decision mapping** — the 4-action space (`SimulationAction`/`BuildDecision`'s
  Hold/EnterLong/EnterShort/Close mapping) and the fixed `CapitalFraction = 0.5`
  are shared, hardcoded logic inside `ComposedPolicyBrain`, not a composable
  piece.
- **Recipe persistence** — a runtime-built brain's *shape* (which encoders +
  architecture + hyperparameters) isn't saved anywhere; only its trained
  weights are, once "Save Model" is used.

## Changelog

- **v1.5** — 2026-07-28 — Added two cheap FX-focused encoders, both pickable
  in the Build Brain panel like any other `EncoderKind`: `SessionClockEncoder`
  (time-of-day + day-of-week as sin/cos pairs -- recurring session structure
  is one of the strongest regularities in FX ticks) and
  `SpreadVolatilityEncoder` (current Bid/Ask spread + a short trailing
  Average True Range). Neither needed a new normalizer: session-clock values
  are already bounded/unitless (passthrough `RawValueNormalizer`), spread/ATR
  are literal price-scaled quantities (passthrough `RawPriceNormalizer`, same
  as `TraderStateEncoder`'s current-price feature). Required extending
  `SimulationObservation` with `Bid`/`Ask` (the exact current-tick quote,
  threaded through from `RlEpisodeRunner.RecordStep`/`SimulationEngine.
  DecideAndApply`, both of which already had them on hand) since neither
  aggregate mode's `CurrentPrice` (a mid-price either way) lets a brain
  recover the spread on its own.
- **v1.4** — 2026-07-28 — Added `CnnArchitecture`/`CnnPolicyNetwork`: a third
  `ArchitectureKind` (`Cnn`, alongside `Mlp`/`Lstm`) pickable in the Build
  Brain panel's Structure card -- a Conv1d stack (configurable kernel size +
  per-layer channel counts) over the same leading-sequence-slice/trailing-
  scalars contract `LstmArchitecture` already established, requiring Bar
  Window the same way LSTM does. `IPolicyGradientBrain.DescribeNetworkShape`
  gained a `Conv1d` case so a CNN brain's Network Activity animation draws its
  conv layers (`out_channels` as each layer's "width", same treatment as
  Linear's `out_features`).
- **v1.3** — 2026-07-28 — `VolumeProfileEncoder`/`TimeProfileEncoder` absorbed
  their former `*RawEncoder` counterparts (deleted) behind a new
  `NormalizationKind` (`Normalized`/`Raw`) build option, and `BarWindowEncoder`
  gained the same option (previously Normalized-only) -- all three now take
  their own fixed `lookbackBars`/`windowSize` at construction, no longer a
  runtime-mutable value shared across Volume/Time Profile via the deleted
  `IHasLookbackBars`/`LookbackAwarePolicyBrain`. See the Train page's Build
  Brain panel doc comment (`BrainBuilder`) and `GLOSSARY.md`'s
  `EncoderSizeOptions` entry for the Train page side of this change.
- **v1.2** — 2026-07-27 — Fixed the Files table's paths (`Heuristic/`,
  `Architectures/` subfolders weren't reflected), added the missing
  `ActionOutputs/` and `CategoricalSampling.cs` rows, and fixed a stray
  `Training/` reference to `Services/Training/`.
- **v1.1** — 2026-07-27 — Adopted the project-wide doc versioning
  convention ([../Docs/README.md](../Docs/README.md)); trimmed the stale
  cross-reference to the old monolithic root README, now replaced by
  [../README.md](../README.md) + [../Docs/Training.md](../Docs/Training.md).
- **v1.0** — original write-up of the composition model (encoders +
  normalizers + architectures), superseding the pre-composition brain
  classes — see git history for detail.
