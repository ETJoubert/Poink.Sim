---
tags: [helper, feature/shared, kind/mixed]
role: helper
feature: Shared
feature_source: shared
kind: mixed
code_lines: 40
types: 3
methods: 4
fan_in: 1
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Components/Shared/WeekRangeGrid/WeekRangeHelper.cs
---
# WeekRangeHelper.cs

One Monday-aligned week of the selectable range, trimmed to the range's own bounds at either end.

**Feature:** Shared · **Kind:** mixed types · **Code lines:** 40 · **Used by:** 1

## Types
- `WeekCell` — record struct, data type (at most one helper method) · 2 properties
- `WeekLabel` — record struct, data type (at most one helper method) · 2 properties
- `WeekRangeHelper` — class, static helper · 4 methods, 1 field

## Uses
- [[Components/Shared/WeekRangeGrid/WeekRangeGrid.razor.md|WeekRangeGrid.razor]]
