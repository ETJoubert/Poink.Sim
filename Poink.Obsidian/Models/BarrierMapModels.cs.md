---
tags: [model, feature/shared, kind/data-bundle]
role: model
feature: Shared
feature_source: shared
kind: data-bundle
code_lines: 57
types: 7
methods: 1
fan_in: 7
fan_out: 0
summary_source: documented
candidates: []
source: Poink.Sim/Models/BarrierMapModels.cs
---
# BarrierMapModels.cs

A barrier map run: a trade opened every EntrySpacingSeconds through the range, on each side, tested against every stop-loss and take-profit size (in pips) and every time limit.

**Feature:** Shared · **Kind:** bundle of data types · **Code lines:** 57 · **Used by:** 7

## Types
- `BarrierMapRequest` — record, data type (at most one helper method) · 8 properties
- `BarrierSide` — enum, enum · 3 values
- `BarrierCell` — record, data type (at most one helper method) · 18 properties
- `BarrierMapGrid` — record, data type (at most one helper method) · 3 properties
- `BarrierMapResult` — record, data type (at most one helper method) · 1 method, 5 properties
- `BarrierMetric` — enum, enum · 5 values
- `BarrierMapSelection` — record, data type (at most one helper method) · 4 properties

## Uses
- _nothing in Poink.Sim_
