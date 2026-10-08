---
tags: [model, feature/training, kind/data, candidate/merge-data]
role: model
feature: Training
feature_source: folder
kind: data
code_lines: 3
types: 1
methods: 0
fan_in: 3
fan_out: 0
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Models/TrainingModels/DataOptionsModel.cs
---
# DataOptionsModel.cs

The Data tab's selected tick source -- cached table, date range, and symbol. Symbol is stored by id; its trading metadata is resolved separately via ISymbolMetadataCache.

**Feature:** Training · **Kind:** data type (at most one helper method) · **Code lines:** 3 · **Used by:** 3

## Types
- `DataOptionsModel` — record, data type (at most one helper method) · 5 properties

## Candidates
- **merge-data** — Merge into `TrainingStrategyModels.cs` with `FeatureModel.cs`, `FeatureOptionsModel.cs`, `FeatureVector.cs`, `NetworkOptionsModel.cs`, `OutputOptionsModel.cs`, `TrainingOptionsModel.cs`.

## Uses
- _nothing in Poink.Sim_
