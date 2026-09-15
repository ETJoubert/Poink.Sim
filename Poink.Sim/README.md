# Poink.Sim

> Doc version: **v2.0** — see "Changelog" at the bottom for what changed.
> Documentation conventions (versioning, changelogs) are in
> [Docs/README.md](Docs/README.md).

Poink.Sim is an always-on Blazor Server dashboard for simulating, training,
and inspecting algorithmic trading strategies against historical tick data.
It was split out of Poink.ML because it isn't a training pipeline — it's an
interactive app with its own lifecycle (play/pause/step/speed against a
live Capital/Position), even though it carries its own TorchSharp/
Microsoft.ML for a trained Brain. It never takes a `ProjectReference` on
Poink.ML: `ITickRepository`/`TickRepository` and the `HlocAvdtBar` bar shape
are each duplicated here rather than shared, so this project's build/deploy
stays independent.

A visual companion to this doc — `Poink.Sim.Architecture.drawio` (open with
[draw.io](https://app.diagrams.net/)) — lives alongside it in this folder.

## Local Data source

C:\Users\etjou\AppData\Roaming\Spotware\Cache\FxPro\BacktestingCache\V1\live_23c56ec5\#UK100\t1

## The four areas

| Area | What it does | Doc |
|---|---|---|
| **Simulation** | Interactive tick-by-tick replay: a live Brain decides, a Trader executes under leveraged CFD mechanics, driven by user play/pause/step/speed control. | [Docs/Simulation.md](Docs/Simulation.md) |
| **Training** | Headless reinforcement learning: a trainable Brain plays thousands of practice episodes against price data and learns from its own trading outcomes. | [Docs/Training.md](Docs/Training.md) |
| **Brain** | The pluggable trading-decision abstraction both Simulation and Training depend on — heuristic rulebooks and trained neural networks behind one shared interface. | [Brain/README.md](Brain/README.md) |
| **Profiler** | Batch (non-playback) chart + Volume/Time Profile viewer for a symbol/date range, with click-to-draw annotations. | [Docs/Profiler.md](Docs/Profiler.md) |

`GLOSSARY.md` is the flat, term-by-term reference index shared across all
four — check there first for an unfamiliar name.

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
playback vs. backward-chunked batch loading). None of the four areas share
UI state — each is its own page(s) and its own DI-registered service(s).

## Shared substrate

- **`Repository/`** — Postgres access: `ITickRepository` (streamed, chunk-
  pruned tick reads, shared by all three data-consuming areas),
  `INetworkRepository` (saved brain topology, no weights), `IModelRepository`
  (trained model snapshots -- weights + run metadata), `ITRangeChartRepository` /
  `ITRangeReferenceChartRepository` (Profiler's saved chart annotations).
- **`Models/`** — Domain and read-model shapes shared across areas
  (`HlocAvdtBar`, `BarPriceHistogram`, `SimulationAccount`,
  `SimulationDecision`, `SimulationSnapshot`, `VolumeProfileAnalysis`,
  `TimeProfileAnalysis`).
- **`Program.cs`** — DI wiring for all four areas: `ITickRepository`/
  `INetworkRepository`/`IModelRepository`/chart repositories and the two fixed
  heuristic Brain instances are singletons shared process-wide (every neural
  Brain instead comes from a saved network, built on demand -- see
  `NetworkService`); `SimulationEngine`/`RlTrainingService` are singletons
  holding one shared in-memory "current run" each; `ProfilerService` is
  scoped (stateless per call, nothing to share across circuits).

## Using it

```
dotnet run --project Poink.Sim
```

Browse to `http://localhost:5001`:

- **`/`** (aliased `/simulation`) — the Simulation page, the app's landing
  page.
- **`/train`** — the Training page.
- **`/profiler`** — the Profiler page.

See each area's doc for what its page actually does.

## Changelog

- **v2.0** — 2026-07-27 — Replaced the monolithic single-file README (which
  had drifted out of sync with the actual `Services/Simulation`,
  `Services/Training`, `Brain/`, `Profiler/` file layout) with this
  four-area abstraction; per-area detail moved to `Docs/`. Introduced the
  documentation versioning convention in `Docs/README.md`.
- **v1.0** — original monolithic README covering Simulation + Training
  inline (superseded — see git history for its content).
