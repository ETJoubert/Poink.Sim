---
tags: [model, feature/training, kind/data, candidate/merge-data]
role: model
feature: Training
feature_source: folder
kind: data
code_lines: 12
types: 1
methods: 0
fan_in: 3
fan_out: 1
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Models/TrainingModels/NetworkOptionsModel.cs
---
# NetworkOptionsModel.cs

The Network tab's architecture settings -- which of MLP/LSTM/CNN's parameters apply depends on ArchitectureKind; the others are simply unused rather than validated against it.

**Feature:** Training · **Kind:** data type (at most one helper method) · **Code lines:** 12 · **Used by:** 3

## Types
- `NetworkOptionsModel` — record, data type (at most one helper method) · 7 properties

## Candidates
- **merge-data** — Merge into `TrainingStrategyModels.cs` with `DataOptionsModel.cs`, `FeatureModel.cs`, `FeatureOptionsModel.cs`, `FeatureVector.cs`, `OutputOptionsModel.cs`, `TrainingOptionsModel.cs`.

## Uses
- [[Services/Brain/Neural/BrainCatalog.cs.md|BrainCatalog.cs]]
