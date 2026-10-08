---
tags: [class, feature/trainingengine, kind/data, candidate/merge-data]
role: class
feature: TrainingEngine
feature_source: folder
kind: data
code_lines: 28
types: 1
methods: 0
fan_in: 3
fan_out: 4
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Services/TrainingEngine/LiveEpisodeSnapshot.cs
---
# LiveEpisodeSnapshot.cs

One decision's worth of in-progress episode state, handed to RunEpisode's optional onDecision callback for the Live tab -- ClosedTrades is a defensive copy of the trader's trade log so far, since the caller may read it from a different thread than the one still appending to it.

**Feature:** TrainingEngine · **Kind:** data type (at most one helper method) · **Code lines:** 28 · **Used by:** 3

## Types
- `LiveEpisodeSnapshot` — record, data type (at most one helper method) · 21 properties

## Candidates
- **merge-data** — Merge into `TrainingRunModels.cs` with `DecisionTrace.cs`, `EpisodeData.cs`, `EpisodeResult.cs`, `LiveTickSnapshot.cs`, `LoadedTrainedModel.cs`, `TraderStateSnapshot.cs`.

## Uses
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
- [[Models/TrainingModels/FeatureModel.cs.md|FeatureModel.cs]]
