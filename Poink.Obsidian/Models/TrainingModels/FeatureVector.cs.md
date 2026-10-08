---
tags: [model, feature/training, kind/data, candidate/merge-data]
role: model
feature: Training
feature_source: folder
kind: data
code_lines: 2
types: 1
methods: 0
fan_in: 2
fan_out: 0
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Models/TrainingModels/FeatureVector.cs
---
# FeatureVector.cs

The network's input vector for one step -- FeatureModel's values flattened to a plain float[] for training.

**Feature:** Training · **Kind:** data type (at most one helper method) · **Code lines:** 2 · **Used by:** 2

## Types
- `FeatureVector` — record, data type (at most one helper method) · 1 property

## Candidates
- **merge-data** — Merge into `TrainingStrategyModels.cs` with `DataOptionsModel.cs`, `FeatureModel.cs`, `FeatureOptionsModel.cs`, `NetworkOptionsModel.cs`, `OutputOptionsModel.cs`, `TrainingOptionsModel.cs`.

## Uses
- _nothing in Poink.Sim_
