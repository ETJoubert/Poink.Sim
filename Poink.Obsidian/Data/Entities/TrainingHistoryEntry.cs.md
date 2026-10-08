---
tags: [class, feature/shared, kind/data]
role: class
feature: Shared
feature_source: shared
kind: data
code_lines: 21
types: 1
methods: 0
fan_in: 3
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Data/Entities/TrainingHistoryEntry.cs
---
# TrainingHistoryEntry.cs

One saved set of training results for a TrainedModel: the run's per-episode metrics and summary stats, with the options it ran under. Deleted with its model.

**Feature:** Shared · **Kind:** data type (at most one helper method) · **Code lines:** 21 · **Used by:** 3

## Types
- `TrainingHistoryEntry` — class, data type (at most one helper method) · 12 properties

## Uses
- [[Components/Pages/Simulation/Helpers/SimulationSymbolPanelBuilder.cs.md|SimulationSymbolPanelBuilder.cs]]
- [[Data/Entities/TrainedModel.cs.md|TrainedModel.cs]]
