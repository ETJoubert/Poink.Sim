---
tags: [model, feature/training, kind/data, candidate/merge-data]
role: model
feature: Training
feature_source: folder
kind: data
code_lines: 9
types: 1
methods: 0
fan_in: 6
fan_out: 5
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Models/TrainingModels/TrainingOptionsModel.cs
---
# TrainingOptionsModel.cs

TrainPageV2's full configuration -- one options model per tab, composed together the way BrainRecipe composes EncoderSizeOptions for the older BrainBuilder-based system. BarWidthSeconds sits here rather than under Features or Training because both read it.

**Feature:** Training · **Kind:** data type (at most one helper method) · **Code lines:** 9 · **Used by:** 6

## Types
- `TrainingOptionsModel` — record, data type (at most one helper method) · 6 properties

## Candidates
- **merge-data** — Merge into `TrainingStrategyModels.cs` with `DataOptionsModel.cs`, `FeatureModel.cs`, `FeatureOptionsModel.cs`, `FeatureVector.cs`, `NetworkOptionsModel.cs`, `OutputOptionsModel.cs`.

## Uses
- [[Models/TrainingModels/DataOptionsModel.cs.md|DataOptionsModel.cs]]
- [[Models/TrainingModels/FeatureOptionsModel.cs.md|FeatureOptionsModel.cs]]
- [[Models/TrainingModels/NetworkOptionsModel.cs.md|NetworkOptionsModel.cs]]
- [[Models/TrainingModels/OutputOptionsModel.cs.md|OutputOptionsModel.cs]]
- [[Models/TrainingModels/TrainingTabOptionsModel.cs.md|TrainingTabOptionsModel.cs]]
