---
tags: [helper, feature/xgrid, kind/static-helper]
role: helper
feature: XGrid
feature_source: folder
kind: static-helper
code_lines: 64
types: 2
methods: 7
fan_in: 1
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/XGrid/Helper/XGridDifferentialHelper.cs
---
# XGridDifferentialHelper.cs

Adapts XGrid selections to the generic differentials panel: each selection becomes a point and a set of tick metrics, and consecutive selections (in click order) become the pairs to measure.

**Feature:** XGrid · **Kind:** static helper · **Code lines:** 64 · **Used by:** 1

## Types
- `XGridDifferentialHelper` — class, static helper · 7 methods
- `Described` — record struct, nested, data type (at most one helper method) · 4 properties

## Uses
- [[Components/Charts/XGrid/Helper/XGridSelection.cs.md|XGridSelection.cs]]
- [[Components/Charts/XGrid/Helper/XGridStats.cs.md|XGridStats.cs]]
- [[Components/Shared/Differentials/DifferentialModels.cs.md|DifferentialModels.cs]]
