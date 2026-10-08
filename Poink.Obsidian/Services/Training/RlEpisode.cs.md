---
tags: [class, feature/training, kind/mixed]
role: class
feature: Training
feature_source: folder
kind: mixed
code_lines: 77
types: 8
methods: 4
fan_in: 21
fan_out: 6
summary_source: generated
candidates: []
source: Poink.Sim/Services/Training/RlEpisode.cs
---
# RlEpisode.cs

Defines rl episode.

**Feature:** Training · **Kind:** mixed types · **Code lines:** 77 · **Used by:** 21

## Types
- `RlStep` — record, data type (at most one helper method) · 5 properties
- `RlBarAction` — record struct, data type (at most one helper method) · 7 properties
- `RlEpisode` — record, data type (at most one helper method) · 18 properties
- `RlEpisodeTrades` — record, data type (at most one helper method) · 7 properties
- `RlEpisodeMetric` — record, data type (at most one helper method) · 3 properties
- `RlEpisodeMetricStats` — class, static helper · 3 methods
- `RlEpisodeWindow` — record, data type (at most one helper method) · 3 properties
- `RlActionCounts` — record, data type (at most one helper method) · 1 method, 5 properties, 1 field

## Uses
- [[Models/BarPriceHistogram.cs.md|BarPriceHistogram.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
- [[Models/SimulationSnapshot.cs.md|SimulationSnapshot.cs]]
- [[Services/Brain/Neural/IPolicyGradientBrain.cs.md|IPolicyGradientBrain.cs]]
