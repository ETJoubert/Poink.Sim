---
tags: [model, feature/shared, kind/data]
role: model
feature: Shared
feature_source: shared
kind: data
code_lines: 19
types: 1
methods: 0
fan_in: 3
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Data/Entities/TrainedModel.cs
---
# TrainedModel.cs

A network trained by the Training Engine: the options it was built and trained with, plus its weights. Saving under an existing name overwrites that model.

**Feature:** Shared · **Kind:** data type (at most one helper method) · **Code lines:** 19 · **Used by:** 3

## Types
- `TrainedModel` — class, data type (at most one helper method) · 9 properties

## Uses
- [[Components/Pages/Simulation/Helpers/SimulationSymbolPanelBuilder.cs.md|SimulationSymbolPanelBuilder.cs]]
- [[Data/Entities/TrainingHistoryEntry.cs.md|TrainingHistoryEntry.cs]]
