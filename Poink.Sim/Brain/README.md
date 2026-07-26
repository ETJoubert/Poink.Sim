# Brain — pluggable trading-decision brains

> This documents the **current, as-built state** of this folder. It supersedes
> the "Machine-learning brain" section of `Poink.Sim/README.md` and the
> `Brain/`-related entries in `Poink.Sim/GLOSSARY.md`, which still describe the
> pre-composition brains (`MlpPolicyBrain`, `LstmPolicyBrain`,
> `VolumeProfilePolicyBrain`, `VolumeProfileBarsPolicyBrain`) that no longer
> exist — those files haven't been updated yet.

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
| **Encoder** — observation → feature vector | `IObservationEncoder` (`FeatureCount`, `SequenceLength`, `Features`, `Encode`) | `BarWindowEncoder` (20-bar percent-change price window), `VolumeProfileEncoder` (20-bucket relative tick-count histogram + POC/value-area summary, implements `IHasLookbackBars`), `TimeProfileEncoder` (20-bucket relative seconds-spent histogram + POC/value-area summary, same `IHasLookbackBars` capability), `VolumeProfileRawEncoder`/`TimeProfileRawEncoder` (same histograms/summaries, unnormalized -- exact tick counts/seconds and exact price levels, also implement `IHasLookbackBars`), `TraderStateEncoder` (12 exact trader/account fields) |
| **Normalizer** — raw quantity → network-ready value (and back, for display) | `IFeatureNormalizer` (`Normalize`, `Denormalize`, `Format`) | `PriceOffsetNormalizer` (percent-change-from-current-price), `EquityRatioNormalizer` (ratio-over-equity, available for injection), `FractionNormalizer` (passthrough for an already-`[0,1]` histogram share), `DirectionNormalizer` (passthrough for a categorical `-1/0/1`), `RawPriceNormalizer`/`RawValueNormalizer` (passthrough for an already-real price/quantity) |
| **Architecture** — feature vector → action logits | `INetworkArchitecture` (`Build(featureCount, sequenceLength)`) | `MlpArchitecture` (flat `Linear→Tanh` stack, any hidden-layer sizes), `LstmArchitecture` (LSTM over a leading sequence slice + trailing scalars, via `LstmPolicyNetwork`) |
| **Combine multiple encoders** | `CompositeEncoder` | Concatenates parts in order; enforces that any sequence-bearing part (`SequenceLength > 0`) is listed **first**, so a recurrent architecture narrow()s the right slice |
| **The brain itself** | `ComposedPolicyBrain` (+ `LookbackAwarePolicyBrain`) | Holds one `IObservationEncoder` + the `Module<Tensor,Tensor>` an architecture built from it; owns the one shared `Decide`/sampling/decision-mapping logic every composed brain uses |

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

`ComposedPolicyBrain.Create(name, architecture, params encoders)` is the only
entry point:

```csharp
ComposedPolicyBrain.Create(
    "Volume Profile + Bars Policy", new MlpArchitecture(64, 32, 16),
    new BarWindowEncoder(), new VolumeProfileEncoder(), new TraderStateEncoder());
```

If one or more encoder parts implement `IHasLookbackBars` (today,
`VolumeProfileEncoder`/`TimeProfileEncoder` and their raw counterparts
`VolumeProfileRawEncoder`/`TimeProfileRawEncoder`), `Create` returns a
`LookbackAwarePolicyBrain` instead of a plain `ComposedPolicyBrain`, so
`brain is IHasLookbackBars` stays precise — absent unless at least one part
actually uses it. Setting `LookbackBars` broadcasts the same value to every
wrapped part (see `Training/RlTrainingService.cs`'s Train-page wiring), so a
brain combining any of these four merges all of them over the same
trailing-bar window rather than needing separate settings.

## Where brains come from

**Fixed at startup** (`Program.cs`) — four named combinations, each one
`ComposedPolicyBrain.Create` call:

| Name | Encoders | Architecture |
|---|---|---|
| `MLP Policy` | Bar Window + Trader State | MLP (32, 16) |
| `LSTM Policy` | Bar Window + Trader State | LSTM (hidden 64, 2 layers) |
| `Volume Profile Policy` | Volume Profile + Trader State | MLP (32, 16) |
| `Volume Profile + Bars Policy` | Bar Window + Volume Profile + Trader State | MLP (64, 32, 16) |

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
| `MomentumThresholdBrain.cs` | Heuristic: opens/holds on a ±threshold price move vs. N observations ago, closes on revert |
| `RandomBrain.cs` | Heuristic: independently rolls whether to act and which direction each decision point — a trivial baseline |
| `BrainRegistry.cs` | Shared mutable brain list backing both `RlTrainingService` and `SimulationEngine`; `Register` adds a runtime-built brain |
| `Neural/IPolicyGradientBrain.cs` | Trained-brain family contract: `Network`, `Encode`, `DecideWithLogProbability`, `DescribeInputs`; default-implements `ExportWeights`/`ImportWeights`/`WeightCount`/`MoveToTrainingDevice`/`MoveToInferenceDevice`/`DescribeNetworkShape` off `Network` alone |
| `Neural/IHasLookbackBars.cs` | Optional capability: a settable `LookbackBars` for an encoder that merges a trailing bar-count window |
| `Neural/Encoders/IObservationEncoder.cs` | Encoder contract: `FeatureCount`, `SequenceLength`, `Features`, `Encode`; also defines `FeatureDescriptor` (`Label` + `IFeatureNormalizer`) |
| `Neural/Encoders/BarWindowEncoder.cs` | Encoder: 20-bar percent-change-from-current-price window |
| `Neural/Encoders/TraderStateEncoder.cs` | Encoder: 12 exact trader/account fields (position direction, position entry price, position ticks held, position cumulative distance, current price, cash, realized/unrealized PnL, equity, margin, margin level, position count) |
| `Neural/Encoders/VolumeProfileEncoder.cs` | Encoder: 20-bucket relative volume (tick-count) histogram + POC/value-area summary; implements `IHasLookbackBars` |
| `Neural/Encoders/TimeProfileEncoder.cs` | Encoder: 20-bucket relative time (seconds-spent) histogram + POC/value-area summary; implements `IHasLookbackBars`; labels prefixed `tp-` so they don't collide with `VolumeProfileEncoder`'s when both are composed together |
| `Neural/Encoders/VolumeProfileRawEncoder.cs` | Encoder: same as `VolumeProfileEncoder` but unnormalized (exact tick counts/prices); implements `IHasLookbackBars`; labels prefixed `vpr-` |
| `Neural/Encoders/TimeProfileRawEncoder.cs` | Encoder: same as `TimeProfileEncoder` but unnormalized (exact seconds/prices); implements `IHasLookbackBars`; labels prefixed `tpr-` |
| `Neural/Encoders/CompositeEncoder.cs` | Concatenates multiple encoders into one, enforcing the sequence-first ordering constraint |
| `Neural/Normalizers/IFeatureNormalizer.cs` | Normalizer contract: `Normalize`, `Denormalize`, `Format`; also defines `DenormalizationAnchors` (`CurrentPrice`/`Equity`) |
| `Neural/Normalizers/PriceOffsetNormalizer.cs` | Normalizer: price ↔ percent-change-from-current-price |
| `Neural/Normalizers/EquityRatioNormalizer.cs` | Normalizer: raw amount ↔ ratio-over-equity |
| `Neural/Normalizers/FractionNormalizer.cs` | Normalizer: passthrough for an already-`[0,1]` histogram-bucket share |
| `Neural/Normalizers/DirectionNormalizer.cs` | Normalizer: passthrough for a categorical `-1/0/1` position flag |
| `Neural/Normalizers/RawPriceNormalizer.cs` | Normalizer: passthrough for an already-real, unscaled price level |
| `Neural/Normalizers/RawValueNormalizer.cs` | Normalizer: passthrough for an already-real, unscaled quantity |
| `Neural/INetworkArchitecture.cs` | Architecture contract: `Build(featureCount, sequenceLength)` |
| `Neural/MlpArchitecture.cs` | Architecture: flat `Linear→Tanh` stack, any hidden-layer sizes |
| `Neural/LstmArchitecture.cs` | Architecture: LSTM over a leading sequence slice + trailing scalars |
| `Neural/LstmPolicyNetwork.cs` | The hand-written `Module<Tensor,Tensor>` `LstmArchitecture` builds (LSTM's tuple-returning `forward` doesn't fit `Sequential`) |
| `Neural/ComposedPolicyBrain.cs` | The one generic brain — pairs an encoder with a network built by an architecture; `LookbackAwarePolicyBrain` is the `IHasLookbackBars`-surfacing subclass |
| `Neural/BrainCatalog.cs` | UI-facing catalog (`EncoderKind`/`ArchitectureKind` enums + factory) the Train page's Brain Builder panel selects from |

## Explicitly not (yet) in this model

- **Training policy** (`IRlTrainingStrategy`, `Training/`) is already a
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
