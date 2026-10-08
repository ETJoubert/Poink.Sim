---
tags: [model, feature/training, kind/data, candidate/move-to-feature]
role: model
feature: Training
feature_source: usage
kind: data
code_lines: 18
types: 1
methods: 0
fan_in: 3
fan_out: 4
summary_source: documented
candidates: [move-to-feature]
source: Poink.Sim/Models/TrainingStartModels.cs
---
# TrainingStartModels.cs

Parameters for starting a training run via ITrainingService.TryStartTraining. A null HistoricRange runs against synthetic (dummy) ticks instead of historic data.

**Feature:** Training (by usage) · **Kind:** data type (at most one helper method) · **Code lines:** 18 · **Used by:** 3

## Types
- `TrainingStartRequest` — record, data type (at most one helper method) · 13 properties

## Candidates
- **move-to-feature** — Used only by the Training feature; move it into that feature's folder.

## Uses
- [[Models/SimulationSnapshot.cs.md|SimulationSnapshot.cs]]
- [[Services/Trader/Trader.cs.md|Trader.cs]]
- [[Services/Trader/TraderSettings.cs.md|TraderSettings.cs]]
- [[Services/Training/EpisodeTimeConstraint.cs.md|EpisodeTimeConstraint.cs]]
