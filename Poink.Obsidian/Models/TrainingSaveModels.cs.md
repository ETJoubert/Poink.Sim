---
tags: [model, feature/training, kind/data, candidate/move-to-feature]
role: model
feature: Training
feature_source: usage
kind: data
code_lines: 12
types: 1
methods: 0
fan_in: 3
fan_out: 2
summary_source: documented
candidates: [move-to-feature]
source: Poink.Sim/Models/TrainingSaveModels.cs
---
# TrainingSaveModels.cs

Parameters for persisting a brain's current weights via ITrainingService.SaveBrain.

**Feature:** Training (by usage) · **Kind:** data type (at most one helper method) · **Code lines:** 12 · **Used by:** 3

## Types
- `TrainingSaveRequest` — record, data type (at most one helper method) · 9 properties

## Candidates
- **move-to-feature** — Used only by the Training feature; move it into that feature's folder.

## Uses
- [[Services/Trader/Trader.cs.md|Trader.cs]]
- [[Services/Trader/TraderSettings.cs.md|TraderSettings.cs]]
