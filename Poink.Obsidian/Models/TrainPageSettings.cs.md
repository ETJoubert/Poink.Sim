---
tags: [settings, feature/training, kind/data, candidate/merge-data, candidate/move-to-feature]
role: settings
feature: Training
feature_source: usage
kind: data
code_lines: 25
types: 1
methods: 0
fan_in: 1
fan_out: 1
summary_source: documented
candidates: [merge-data, move-to-feature]
source: Poink.Sim/Models/TrainPageSettings.cs
---
# TrainPageSettings.cs

Snapshot of the Train page's Run Setup config (Data Range/Brain/Strategy/Episode Timing/Trader), persisted to browser localStorage so it survives navigating away and back -- IRlTrainingService keeps a run going across navigation, but the page's own fields are reset to their defaults every time Blazor recreates the…

**Feature:** Training (by usage) · **Kind:** data type (at most one helper method) · **Code lines:** 25 · **Used by:** 1

## Types
- `TrainPageSettings` — record, data type (at most one helper method) · 20 properties

## Candidates
- **merge-data** — Merge into `TrainingResultModels.cs`.
- **move-to-feature** — Used only by the Training feature; move it into that feature's folder.

## Uses
- [[Models/SimulationSnapshot.cs.md|SimulationSnapshot.cs]]
