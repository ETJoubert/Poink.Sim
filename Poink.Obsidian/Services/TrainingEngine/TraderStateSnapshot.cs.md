---
tags: [class, feature/trainingengine, kind/data, candidate/merge-data]
role: class
feature: TrainingEngine
feature_source: folder
kind: data
code_lines: 3
types: 1
methods: 0
fan_in: 2
fan_out: 0
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Services/TrainingEngine/TraderStateSnapshot.cs
---
# TraderStateSnapshot.cs

Raw trader/account values for one step, before TraderStateOptionsModel decides whether/how much of it is actually included in the feature vector. All percentages are of cash balance, not a raw money amount -- zero both when flat and when flat P/L.

**Feature:** TrainingEngine · **Kind:** data type (at most one helper method) · **Code lines:** 3 · **Used by:** 2

## Types
- `TraderStateSnapshot` — record, data type (at most one helper method) · 3 properties

## Candidates
- **merge-data** — Merge into `TrainingRunModels.cs` with `DecisionTrace.cs`, `EpisodeData.cs`, `EpisodeResult.cs`, `LiveEpisodeSnapshot.cs`, `LiveTickSnapshot.cs`, `LoadedTrainedModel.cs`.

## Uses
- _nothing in Poink.Sim_
