---
tags: [model, feature/training, kind/data-bundle]
role: model
feature: Training
feature_source: folder
kind: data-bundle
code_lines: 43
types: 6
methods: 0
fan_in: 7
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Models/TrainingModels/TrainingTabOptionsModel.cs
---
# TrainingTabOptionsModel.cs

The Training tab's episode timing, trader setup, learning, and decision masking settings.

**Feature:** Training · **Kind:** bundle of data types · **Code lines:** 43 · **Used by:** 7

## Types
- `EpisodeSamplingMode` — enum, enum · 3 values
- `EpisodeTimingOptionsModel` — record, data type (at most one helper method) · 10 properties
- `TraderSetupOptionsModel` — record, data type (at most one helper method) · 6 properties
- `LearningOptionsModel` — record, data type (at most one helper method) · 3 properties
- `DecisionMaskingOptionsModel` — record, data type (at most one helper method) · 2 properties
- `TrainingTabOptionsModel` — record, data type (at most one helper method) · 4 properties

## Uses
- [[Models/TrainingModels/PositionControlModels.cs.md|PositionControlModels.cs]]
- [[Models/TrainingModels/TrainingStrategyModels.cs.md|TrainingStrategyModels.cs]]
- [[Services/Brain/Neural/BrainCatalog.cs.md|BrainCatalog.cs]]
