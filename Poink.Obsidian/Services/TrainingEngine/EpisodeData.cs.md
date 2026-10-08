---
tags: [class, feature/trainingengine, kind/data, candidate/merge-data]
role: class
feature: TrainingEngine
feature_source: folder
kind: data
code_lines: 6
types: 1
methods: 0
fan_in: 1
fan_out: 1
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Services/TrainingEngine/EpisodeData.cs
---
# EpisodeData.cs

One episode's ticks. Everything before TrainingStartIndex is the preloaded feature window: it feeds features and bars but is never traded or learned from.

**Feature:** TrainingEngine · **Kind:** data type (at most one helper method) · **Code lines:** 6 · **Used by:** 1

## Types
- `EpisodeData` — record, data type (at most one helper method) · 4 properties

## Candidates
- **merge-data** — Merge into `TrainingRunModels.cs` with `DecisionTrace.cs`, `EpisodeResult.cs`, `LiveEpisodeSnapshot.cs`, `LiveTickSnapshot.cs`, `LoadedTrainedModel.cs`, `TraderStateSnapshot.cs`.

## Uses
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
