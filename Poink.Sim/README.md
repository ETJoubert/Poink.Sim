# Poink.Sim

Poink.Sim is an always-on Blazor Server dashboard for simulating, training,
and inspecting algorithmic trading strategies against historical tick data.
See the [root README](../README.md) for setup.

A visual companion to this doc — `Docs/Poink.Sim.Architecture.drawio` (open with
[draw.io](https://app.diagrams.net/)) — lives in this folder.

## Core areas

| Area | What it does |
|---|---|
| **Simulation** | Interactive tick-by-tick replay: a live Brain decides, a Trader executes under leveraged CFD mechanics, driven by user play/pause/step/speed control. |
| **Training** | Headless reinforcement learning: a trainable Brain plays thousands of practice episodes against price data and learns from its own trading outcomes. |
| **Brain** | The pluggable trading-decision abstraction both Simulation and Training depend on — heuristic rulebooks and trained neural networks behind one shared interface. See [Services/Brain/README.md](Services/Brain/README.md). |
| **Profiler** | Batch (non-playback) chart + Volume/Time Profile viewer for a symbol/date range, with click-to-draw annotations. |

## How they relate

```
                 ┌───────────────┐
                 │ ITickRepository│  (Postgres: ticks_<symbol> tables)
                 └───────┬───────┘
        ┌────────────────┼────────────────┐
        │                │                │
        ▼                ▼                ▼
  ┌───────────┐   ┌──────────────┐  ┌───────────┐
  │ Simulation│   │   Training   │  │  Profiler │
  │ (live     │   │  (headless   │  │  (batch   │
  │ playback) │   │   episodes)  │  │  viewer)  │
  └─────┬─────┘   └──────┬───────┘  └───────────┘
        │                │
        └────────┬───────┘
                 ▼
           ┌───────────┐
           │   Brain   │  (ISimulationBrain / IPolicyGradientBrain)
           └───────────┘
```

Simulation and Training both consume the same Brain abstraction — Training
mutates a trainable Brain's live weights in place; Simulation immediately
reflects whatever that Brain has learned so far, since it's the same DI
singleton instance in both. Simulation and Profiler share the same
tick→bar aggregation (`SimulationBarAggregator`) and Volume/Time Profile
computation, just under different loading strategies (live streaming
playback vs. backward-chunked batch loading). None of the areas share
UI state — each is its own page(s) and its own DI-registered service(s).

## Shared substrate

- **`Repository/`** — Postgres access: `ITickRepository` (streamed, chunk-
  pruned tick reads, shared by all three data-consuming areas),
  `INetworkRepository` (saved brain topology, no weights), `IModelRepository`
  (trained model snapshots -- weights + run metadata), `ITRangeChartRepository` /
  `ITRangeReferenceChartRepository` (Profiler's saved chart annotations).
- **`Models/`** — Domain and read-model shapes shared across areas
  (`HlocAvdtBar`, `BarPriceHistogram`, `SimulationAccount`,
  `SimulationDecision`, `SimulationSnapshot`, `VolumeProfileAnalysis`).
- **`Helpers/`** — Pure calculation helpers shared across areas
  (`RangeStatsHelper`/`VectorStatsHelper` for scalar/vector tick and bar
  measurements, `BarAggregator`, `TimeProfileHelper`).
- **`Program.cs`** — DI wiring for all areas: `ITickRepository`/
  `INetworkRepository`/`IModelRepository`/chart repositories and the two fixed
  heuristic Brain instances are singletons shared process-wide (every neural
  Brain instead comes from a saved network, built on demand -- see
  `NetworkService`); `SimulationEngine`/`RlTrainingService` are singletons
  holding one shared in-memory "current run" each; `ProfilerService` is
  scoped (stateless per call, nothing to share across circuits).

## Pages

Browse to `http://localhost:5001`:

| Route | Page |
|---|---|
| `/` (also `/simulation`) | Simulation (landing page) |
| `/train`, `/train2` | Training (classic and tick-based engine) |
| `/profiler` | Profiler |
| `/signal-study` | Signal Study |
| `/trade` | Trade |
| `/data` | Data |
| `/analize` | Analyze |
| `/emulator` | Emulator |
| `/code`, `/arch`, `/calls`, `/trade-map` | Reflection views of the codebase |
