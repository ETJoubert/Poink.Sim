---
tags: [model, feature/training, kind/data-bundle, candidate/merge-data]
role: model
feature: Training
feature_source: folder
kind: data-bundle
code_lines: 21
types: 4
methods: 0
fan_in: 4
fan_out: 1
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Models/TrainingModels/OutputOptionsModel.cs
---
# OutputOptionsModel.cs

The Output tab's action set, masking rules, and decoding settings.

**Feature:** Training · **Kind:** bundle of data types · **Code lines:** 21 · **Used by:** 4

## Types
- `ActionsOptionsModel` — record, data type (at most one helper method) · 4 properties
- `ActionMaskingOptionsModel` — record, data type (at most one helper method) · 2 properties
- `ActionDecodingOptionsModel` — record, data type (at most one helper method) · 2 properties
- `OutputOptionsModel` — record, data type (at most one helper method) · 3 properties

## Candidates
- **merge-data** — Merge into `TrainingStrategyModels.cs` with `DataOptionsModel.cs`, `FeatureModel.cs`, `FeatureOptionsModel.cs`, `FeatureVector.cs`, `NetworkOptionsModel.cs`, `TrainingOptionsModel.cs`.

## Uses
- [[Services/Brain/Neural/BrainCatalog.cs.md|BrainCatalog.cs]]
