---
tags: [class, feature/trainingengine, kind/data, candidate/merge-data]
role: class
feature: TrainingEngine
feature_source: folder
kind: data
code_lines: 18
types: 1
methods: 0
fan_in: 3
fan_out: 2
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Services/TrainingEngine/LiveTickSnapshot.cs
---
# LiveTickSnapshot.cs

The market and account state after one tick, handed to RunEpisode's optional onTick callback for the Live tab. ClosedTrades is only a copy when a trade closed on this tick, otherwise null.

**Feature:** TrainingEngine · **Kind:** data type (at most one helper method) · **Code lines:** 18 · **Used by:** 3

## Types
- `LiveTickSnapshot` — record, data type (at most one helper method) · 12 properties

## Candidates
- **merge-data** — Merge into `TrainingRunModels.cs` with `DecisionTrace.cs`, `EpisodeData.cs`, `EpisodeResult.cs`, `LiveEpisodeSnapshot.cs`, `LoadedTrainedModel.cs`, `TraderStateSnapshot.cs`.

## Uses
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
