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
| **Encoder** — observation → feature vector | `IObservationEncoder` (`FeatureCount`, `SequenceLength`, `FeatureLabels`, `Encode`) | `BarWindowEncoder` (20-bar percent-change price window), `VolumeProfileEncoder` (20-bucket tick-count histogram + POC/value-area summary, implements `IHasLookbackBars`), `TimeProfileEncoder` (20-bucket seconds-spent histogram + POC/value-area summary, same `IHasLookbackBars` capability), `CapitalStateEncoder` (4 bounded capital/position ratios) |
| **Architecture** — feature vector → action logits | `INetworkArchitecture` (`Build(featureCount, sequenceLength)`) | `MlpArchitecture` (flat `Linear→Tanh` stack, any hidden-layer sizes), `LstmArchitecture` (LSTM over a leading sequence slice + trailing scalars, via `LstmPolicyNetwork`) |
| **Combine multiple encoders** | `CompositeEncoder` | Concatenates parts in order; enforces that any sequence-bearing part (`SequenceLength > 0`) is listed **first**, so a recurrent architecture narrow()s the right slice |
| **The brain itself** | `ComposedPolicyBrain` (+ `LookbackAwarePolicyBrain`) | Holds one `IObservationEncoder` + the `Module<Tensor,Tensor>` an architecture built from it; owns the one shared `Decide`/sampling/decision-mapping logic every composed brain uses |

`ComposedPolicyBrain.Create(name, architecture, params encoders)` is the only
entry point:

```csharp
ComposedPolicyBrain.Create(
    "Volume Profile + Bars Policy", new MlpArchitecture(64, 32, 16),
    new BarWindowEncoder(), new VolumeProfileEncoder(), new CapitalStateEncoder());
```

If one or more encoder parts implement `IHasLookbackBars` (today,
`VolumeProfileEncoder` and `TimeProfileEncoder`), `Create` returns a
`LookbackAwarePolicyBrain` instead of a plain `ComposedPolicyBrain`, so
`brain is IHasLookbackBars` stays precise — absent unless at least one part
actually uses it. Setting `LookbackBars` broadcasts the same value to every
wrapped part (see `Training/RlTrainingService.cs`'s Train-page wiring), so a
brain combining both Volume Profile and Time Profile merges both over the
same trailing-bar window rather than needing two separate settings.

## Where brains come from

**Fixed at startup** (`Program.cs`) — four named combinations, each one
`ComposedPolicyBrain.Create` call:

| Name | Encoders | Architecture |
|---|---|---|
| `MLP Policy` | Bar Window + Capital State | MLP (32, 16) |
| `LSTM Policy` | Bar Window + Capital State | LSTM (hidden 64, 2 layers) |
| `Volume Profile Policy` | Volume Profile + Capital State | MLP (32, 16) |
| `Volume Profile + Bars Policy` | Bar Window + Volume Profile + Capital State | MLP (64, 32, 16) |

**Built at runtime** — the Train page's "+ BUILD BRAIN" panel
(`Components/Pages/Training/Pages/TrainPage.razor`) lets a user pick any
encoder combination and architecture, then calls
`RlTrainingService.TryBuildBrain`, which resolves the pick through
`BrainCatalog` (the enum-driven `EncoderKind`/`ArchitectureKind` factory) and
registers the result into `BrainRegistry`. `CapitalStateEncoder` is always
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
| `Neural/Encoders/IObservationEncoder.cs` | Encoder contract: `FeatureCount`, `SequenceLength`, `FeatureLabels`, `Encode` |
| `Neural/Encoders/BarWindowEncoder.cs` | Encoder: 20-bar percent-change-from-current-price window |
| `Neural/Encoders/CapitalStateEncoder.cs` | Encoder: 4 bounded capital/position ratios |
| `Neural/Encoders/VolumeProfileEncoder.cs` | Encoder: 20-bucket relative volume (tick-count) histogram + POC/value-area summary; implements `IHasLookbackBars` |
| `Neural/Encoders/TimeProfileEncoder.cs` | Encoder: 20-bucket relative time (seconds-spent) histogram + POC/value-area summary; implements `IHasLookbackBars`; labels prefixed `tp-` so they don't collide with `VolumeProfileEncoder`'s when both are composed together |
| `Neural/Encoders/CompositeEncoder.cs` | Concatenates multiple encoders into one, enforcing the sequence-first ordering constraint |
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
