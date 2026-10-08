---
tags: [model, feature/training, kind/data-bundle, candidate/move-to-feature]
role: model
feature: Training
feature_source: usage
kind: data-bundle
code_lines: 62
types: 5
methods: 0
fan_in: 4
fan_out: 2
summary_source: documented
candidates: [move-to-feature]
source: Poink.Sim/Models/TrainingResultModels.cs
---
# TrainingResultModels.cs

Snapshot of a training run's Run Setup config (Data Range/Brain/Strategy/Episode Timing/Trader), taken at save time -- decoupled from TrainPageSettings, which persists the page's live UI state rather than a saved result.

**Feature:** Training (by usage) · **Kind:** bundle of data types · **Code lines:** 62 · **Used by:** 4

## Types
- `TrainingConfig` — record, data type (at most one helper method) · 20 properties
- `TrainingSessionStats` — record, data type (at most one helper method) · 11 properties
- `TrainingResultsGraph` — record, data type (at most one helper method) · 2 properties
- `TrainingResultModel` — record, data type (at most one helper method) · 6 properties
- `TrainingResultSummary` — record, data type (at most one helper method) · 9 properties

## Candidates
- **move-to-feature** — Used only by the Training feature; move it into that feature's folder.

## Uses
- [[Models/SimulationSnapshot.cs.md|SimulationSnapshot.cs]]
- [[Services/Training/RlEpisode.cs.md|RlEpisode.cs]]
