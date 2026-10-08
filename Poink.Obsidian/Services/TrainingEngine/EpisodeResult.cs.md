---
tags: [class, feature/trainingengine, kind/data-bundle, candidate/merge-data]
role: class
feature: TrainingEngine
feature_source: folder
kind: data-bundle
code_lines: 7
types: 2
methods: 0
fan_in: 1
fan_out: 2
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Services/TrainingEngine/EpisodeResult.cs
---
# EpisodeResult.cs

The full outcome of one episode rollout -- Reward is the same equity-drift sum RunEpisode's caller would need for a future learning step.

**Feature:** TrainingEngine · **Kind:** bundle of data types · **Code lines:** 7 · **Used by:** 1

## Types
- `EpisodeStep` — record struct, data type (at most one helper method) · 4 properties
- `EpisodeResult` — record, data type (at most one helper method) · 4 properties, 1 field

## Candidates
- **merge-data** — Merge into `TrainingRunModels.cs` with `DecisionTrace.cs`, `EpisodeData.cs`, `LiveEpisodeSnapshot.cs`, `LiveTickSnapshot.cs`, `LoadedTrainedModel.cs`, `TraderStateSnapshot.cs`.

## Uses
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
