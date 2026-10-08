---
tags: [model, feature/activity, kind/data-bundle]
role: model
feature: Activity
feature_source: folder
kind: data-bundle
code_lines: 15
types: 5
methods: 0
fan_in: 12
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Services/Activity/ActivityModels.cs
---
# ActivityModels.cs

How a background run ended; only Completed and Failed call for attention.

**Feature:** Activity · **Kind:** bundle of data types · **Code lines:** 15 · **Used by:** 12

## Types
- `ActivityOutcome` — enum, enum · 4 values
- `ActivityState` — record, data type (at most one helper method) · 2 properties, 1 field
- `ActivityResult` — record, data type (at most one helper method) · 2 properties
- `ActivityCompletion` — record, data type (at most one helper method) · 6 properties
- `RouteActivity` — record, data type (at most one helper method) · 4 properties, 1 field

## Uses
- [[Services/Analysis/BarrierMapCalculator.cs.md|BarrierMapCalculator.cs]]
