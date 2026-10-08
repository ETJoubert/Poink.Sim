---
tags: [model, feature/trainingengine, kind/data, candidate/merge-data]
role: model
feature: TrainingEngine
feature_source: folder
kind: data
code_lines: 3
types: 1
methods: 0
fan_in: 1
fan_out: 4
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Services/TrainingEngine/LoadedTrainedModel.cs
---
# LoadedTrainedModel.cs

A saved model rebuilt for training: its options, the network with its saved weights, and the action space those options produce.

**Feature:** TrainingEngine · **Kind:** data type (at most one helper method) · **Code lines:** 3 · **Used by:** 1

## Types
- `LoadedTrainedModel` — record, data type (at most one helper method) · 4 properties

## Candidates
- **merge-data** — Merge into `TrainingRunModels.cs` with `DecisionTrace.cs`, `EpisodeData.cs`, `EpisodeResult.cs`, `LiveEpisodeSnapshot.cs`, `LiveTickSnapshot.cs`, `TraderStateSnapshot.cs`.

## Uses
- [[Models/TrainingModels/TrainedModelModels.cs.md|TrainedModelModels.cs]]
- [[Models/TrainingModels/TrainingOptionsModel.cs.md|TrainingOptionsModel.cs]]
- [[Services/TrainingEngine/ActionSpace.cs.md|ActionSpace.cs]]
- [[Services/TrainingEngine/MlpNetwork.cs.md|MlpNetwork.cs]]
