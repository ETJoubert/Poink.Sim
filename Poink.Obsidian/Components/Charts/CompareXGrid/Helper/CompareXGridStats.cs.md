---
tags: [class, feature/comparexgrid, kind/mixed]
role: class
feature: CompareXGrid
feature_source: folder
kind: mixed
code_lines: 66
types: 2
methods: 2
fan_in: 3
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/CompareXGrid/Helper/CompareXGridStats.cs
---
# CompareXGridStats.cs

Builds a CompareXGridResult: every symbol's histogram ticks inside [start, end] binned into % rows from its own reference price and into equal time columns, so differently priced symbols line up cell for cell.

**Feature:** CompareXGrid · **Kind:** mixed types · **Code lines:** 66 · **Used by:** 3

## Types
- `CompareXGridInput` — record, data type (at most one helper method) · 5 properties
- `CompareXGridStats` — class, static helper · 2 methods, 1 field

## Uses
- [[Components/Charts/CompareXGrid/CompareXGridModels.cs.md|CompareXGridModels.cs]]
- [[Models/BarPriceHistogram.cs.md|BarPriceHistogram.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
