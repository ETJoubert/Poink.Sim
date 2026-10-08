---
tags: [model, feature/training, kind/data-bundle]
role: model
feature: Training
feature_source: folder
kind: data-bundle
code_lines: 27
types: 5
methods: 0
fan_in: 8
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Models/TrainingModels/TrainedModelModels.cs
---
# TrainedModelModels.cs

A saved model's listing row -- everything except its config and weights.

**Feature:** Training · **Kind:** bundle of data types · **Code lines:** 27 · **Used by:** 8

## Types
- `TrainedModelSummary` — record, data type (at most one helper method) · 6 properties
- `TrainedModelData` — record, data type (at most one helper method) · 3 properties
- `TrainingHistorySummary` — record, data type (at most one helper method) · 10 properties
- `TrainingHistoryData` — record, data type (at most one helper method) · 3 properties
- `TrainingRunSnapshot` — record, data type (at most one helper method) · 6 properties

## Uses
- [[Services/Training/RlEpisode.cs.md|RlEpisode.cs]]
