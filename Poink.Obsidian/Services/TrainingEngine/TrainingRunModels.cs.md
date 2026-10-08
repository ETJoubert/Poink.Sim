---
tags: [model, feature/trainingengine, kind/data]
role: model
feature: TrainingEngine
feature_source: folder
kind: data
code_lines: 10
types: 1
methods: 0
fan_in: 2
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Services/TrainingEngine/TrainingRunModels.cs
---
# TrainingRunModels.cs

The page watching a TrainingRunService run. EpisodeStarting fires before every episode; IsLive is read once per episode, and when false (or no observer is attached) the episode runs headless at full speed with no live callbacks.

**Feature:** TrainingEngine · **Kind:** data type (at most one helper method) · **Code lines:** 10 · **Used by:** 2

## Types
- `TrainingRunObserver` — record, data type (at most one helper method) · 6 properties

## Uses
- [[Services/TrainingEngine/LiveEpisodeSnapshot.cs.md|LiveEpisodeSnapshot.cs]]
- [[Services/TrainingEngine/LiveTickSnapshot.cs.md|LiveTickSnapshot.cs]]
