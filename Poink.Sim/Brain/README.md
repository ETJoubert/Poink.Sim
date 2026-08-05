### ABSTRACT

A brain is a function from market state to a trading action, learned by trial.

Everything here is one loop. History is replayed as ticks. A slice of that history becomes an observation. The observation is encoded into numbers, the numbers run through a network, and the network's output is collapsed into a single action. A trader executes that action against a simulated account. The change in the account is scored, and the score is pushed back through the network so the next pass is slightly better.

```
Data → Inputs → Network → Outputs → Actions → Trader → Simulation → Rewards
  ↑                                                                    │
  └────────────────────────────────────────────────────────────────────┘
```

Two properties hold the design together:

- **Every stage is swappable.** Encoders, architecture, action heads, reward policy and training strategy are each an interface with several implementations. A brain is a recipe naming which ones to use, so a new idea is a new implementation, not a new pipeline.
- **Nothing is live.** The loop only ever sees recorded ticks against a simulated account. Reward is the only signal, and it comes from the account, so an idea is only as good as the equity it produces.

## Overview

The brain object in essense is a network with inputs that learns to provide outputs.
The Data are sampled into episodes.
The inputs are transforms of a sub set of the episode.
The network is built from layers and a chosen architecture.
The outputs are decoded to collapse to an outcome.
The outputs are linked to actions.
The actions are performed by a Trader.
The trader performs these actions on a simulation.
The outcomes of the actions by the trader are rewarded.
The rewards are fed back into the network to learn.

The **Code** page (`/code`) draws all of this as a live circuit: every object, its named ports, and the wires between them.

## Data

Ticks in, bars out.

### Tick Source
`ITickRepository` reads `ticks_<symbol>` from Postgres. `DummyTickGenerator` stands in when there is no database.

### Episode Sampler
`EpisodeWindowSampler` picks the random window for one episode. `EpisodeTimeConstraint` limits it to the weekdays and session hours you allow.

### Bar Aggregator
`SimulationBarAggregator` folds ticks into fixed-width bars, each with a price histogram.

`IEnumerable<Tick>` → `List<Tick>` → `(HlocAvdtBar, BarPriceHistogram)`

## Inputs

The observation, flattened to numbers.

### Observation
`SimulationObservation` — recent bars, ticks, histograms, capital, position, trade log.

### Encoders
Six `IObservationEncoder` implementations: bar window, volume profile, time profile, session clock, spread/volatility, trader state. `CompositeEncoder` concatenates the enabled ones, sequence-bearing encoder first.

### Normalizers
Nine `IFeatureNormalizer` implementations, scaling each feature so prices, spreads and flags arrive comparable.

`SimulationObservation` → `float[]`

## Brain

The composed policy object.

### Composition
`ComposedPolicyBrain` binds encoders + architecture + action heads into one `IPolicyGradientBrain`. It encodes the observation, runs the network, and decodes the result into a decision with its log probability.

### Recipe
`BrainRecipe` is the blueprint. `BrainCatalog` builds from it, `BrainRegistry` holds the live instances, `ModelService` saves and loads weights.

`float[]` → `Tensor [1, FeatureCount]`

## Network

The trainable part.

### Architectures
`INetworkArchitecture` builds the TorchSharp module — `MlpArchitecture`, `LstmArchitecture`, `CnnArchitecture`.

### Shape
One flat logit vector: the enabled directions first, then each action head's slice.

`Tensor [1, FeatureCount]` → `Tensor [1, outputWidth]`

## Outputs

Logits sliced and collapsed.

### Decoder
The direction slice goes through `CategoricalSampling` — softmax sample or hard argmax, temperature scaled. Yields the action index, its log probability, and the entropy.

### Action Heads
Each `IActionOutputHead` owns the next contiguous slice and samples a discrete level. `DiscretizedLevelsActionHead` covers volume fraction, stop loss and take profit, as fractions of the current price.

logits → `action index`, `head values`, `log probability`, `entropy`

## Actions

### Direction
`SimulationAction` — `Hold`, `EnterLong`, `EnterShort`, `Close`.

### Decision
`SimulationDecision` adds capital fraction, stop loss and take profit prices, and order type.

An Enter while a position is open, or a Close with none, is recorded as **blocked**. It still counts as an action.

## Trader

`ITrader` executes decisions against the account.

### Per tick
`Advance` runs stop loss, take profit, margin stop-out, then any resting `PendingOrder`. `Apply` opens or closes.

### Sizing
`notional = capitalFraction × Cash × Leverage`. Commission is charged on open **and** close.

### Settings
`TraderSettings` — leverage, stop-out level, `ICommissionModel`.

## Simulation

Two drivers over the same core.

### Training
`RlEpisodeRunner` loops one episode's ticks headlessly and returns an `RlEpisode`.

### Interactive
`SimulationEngine` replays with play, pause, step and speed. Drives the Simulation page.

### Outer loop
`RlTrainingService` — sample window, prefetch ticks, run the episode, train on it, repeat.

## Rewards

### Reward Policy
`IRewardPolicy` scores each step from the capital and position change: `EquityDrift` (default), `TradingRules`, `DifferentialSharpe`, `MarketStructure`. The reward attaches to the **previous** step — one step of lag.

### Training Strategy
`PolicyGradientTrainingStrategy` turns the finished episode into a weight update: discount returns (γ 0.99), normalize, REINFORCE loss with an entropy bonus annealed by run progress, Adam step.

That step writes into `Network.parameters()`, which closes the loop.
