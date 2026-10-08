---
tags: [model, feature/training, kind/data-bundle, candidate/merge-data]
role: model
feature: Training
feature_source: folder
kind: data-bundle
code_lines: 12
types: 4
methods: 0
fan_in: 3
fan_out: 2
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Models/TrainingModels/FeatureModel.cs
---
# FeatureModel.cs

The Features tab's computed values for one step, structured (not flattened) so a display panel can walk it via reflection and show each feature's label and value -- FeatureVector is built from this for actual training.

**Feature:** Training · **Kind:** bundle of data types · **Code lines:** 12 · **Used by:** 3

## Types
- `XGridSignalsFeature` — record, data type (at most one helper method) · 1 property
- `TxFoldFeature` — record, data type (at most one helper method) · 4 properties
- `TraderStateFeature` — record, data type (at most one helper method) · 3 properties
- `FeatureModel` — record, data type (at most one helper method) · 3 properties

## Candidates
- **merge-data** — Merge into `TrainingStrategyModels.cs` with `DataOptionsModel.cs`, `FeatureOptionsModel.cs`, `FeatureVector.cs`, `NetworkOptionsModel.cs`, `OutputOptionsModel.cs`, `TrainingOptionsModel.cs`.

## Uses
- [[Components/Charts/TxFold/Helper/TxFoldLevelOptions.cs.md|TxFoldLevelOptions.cs]]
- [[Components/Charts/TxFold/TxFold.razor.md|TxFold.razor]]
