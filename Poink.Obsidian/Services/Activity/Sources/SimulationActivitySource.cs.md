---
tags: [class, feature/activity, kind/behavior]
role: class
feature: Activity
feature_source: folder
kind: behavior
code_lines: 27
types: 1
methods: 2
fan_in: 1
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Services/Activity/Sources/SimulationActivitySource.cs
---
# SimulationActivitySource.cs

Reports simulation playback; a pause is silent, while reaching the end or a depletion stop counts as completed.

**Feature:** Activity · **Kind:** class with behaviour · **Code lines:** 27 · **Used by:** 1

## Types
- `SimulationActivitySource` — class, class with behaviour · 2 methods, 4 properties, 1 field, 1 constructor, 1 event

## Uses
- [[Services/Activity/ActivityModels.cs.md|ActivityModels.cs]]
- [[Services/Activity/IActivitySource.cs.md|IActivitySource.cs]]
- [[Services/Simulation/ISimulationEngine.cs.md|ISimulationEngine.cs]]
