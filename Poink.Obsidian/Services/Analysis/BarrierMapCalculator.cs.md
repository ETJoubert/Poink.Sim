---
tags: [class, feature/analysis, kind/behavior]
role: class
feature: Analysis
feature_source: folder
kind: behavior
code_lines: 290
types: 7
methods: 19
fan_in: 2
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Services/Analysis/BarrierMapCalculator.cs
---
# BarrierMapCalculator.cs

Computes a barrier map over ticks held in memory. Each entry is scanned once per side, noting the first moment every stop and every target size is reached and the open result at every time limit; every stop/target/limit cell is then decided from those, so the grid size barely affects the cost.

**Feature:** Analysis · **Kind:** class with behaviour · **Code lines:** 290 · **Used by:** 2

## Types
- `BarrierMapCalculator` — class, class with behaviour · 9 methods, 5 fields, 1 constructor
- `Outcome` — enum, nested, enum · 3 values
- `PricePath` — class, nested, single-method class · 1 method, 2 properties, 1 constructor
- `GridLayout` — class, nested, class with behaviour · 2 methods, 8 properties, 1 constructor
- `SideAccumulators` — class, nested, class with behaviour · 2 methods, 2 properties, 1 constructor
- `EntryScratch` — class, nested, data type (at most one helper method) · 1 method, 5 properties, 1 constructor
- `CellAccumulator` — class, nested, class with behaviour · 4 methods, 11 properties, 6 fields, 1 constructor

## Uses
- [[Models/BarrierMapModels.cs.md|BarrierMapModels.cs]]
