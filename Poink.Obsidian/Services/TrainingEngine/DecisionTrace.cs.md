---
tags: [class, feature/trainingengine, kind/data, candidate/merge-data]
role: class
feature: TrainingEngine
feature_source: folder
kind: data
code_lines: 3
types: 1
methods: 0
fan_in: 3
fan_out: 0
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Services/TrainingEngine/DecisionTrace.cs
---
# DecisionTrace.cs

Everything PolicyGradientLearner needs from one decision step -- Activations comes straight from ForwardWithActivations so it never has to recompute a forward pass.

**Feature:** TrainingEngine · **Kind:** data type (at most one helper method) · **Code lines:** 3 · **Used by:** 3

## Types
- `DecisionTrace` — record, data type (at most one helper method) · 4 properties

## Candidates
- **merge-data** — Merge into `TrainingRunModels.cs` with `EpisodeData.cs`, `EpisodeResult.cs`, `LiveEpisodeSnapshot.cs`, `LiveTickSnapshot.cs`, `LoadedTrainedModel.cs`, `TraderStateSnapshot.cs`.

## Uses
- _nothing in Poink.Sim_
