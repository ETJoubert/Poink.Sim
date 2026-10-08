---
tags: [model, feature/profiler, kind/data-bundle]
role: model
feature: Profiler
feature_source: folder
kind: data-bundle
code_lines: 31
types: 5
methods: 0
fan_in: 5
fan_out: 5
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Profiler/Models/ProfilerCompareModels.cs
---
# ProfilerCompareModels.cs

What the Profiler's compare chart measures each symbol's % change from.

**Feature:** Profiler · **Kind:** bundle of data types · **Code lines:** 31 · **Used by:** 5

## Types
- `ProfilerCompareMode` — enum, enum · 2 values
- `ProfilerCompareAnchor` — enum, enum · 2 values
- `ProfilerCompareXGridSettings` — record, data type (at most one helper method) · 8 properties
- `ProfilerCompareTxFoldSettings` — record, data type (at most one helper method) · 5 properties
- `ProfilerComparePanel` — enum, enum · 3 values

## Uses
- [[Components/Charts/CompareTxFold/CompareTxFoldModels.cs.md|CompareTxFoldModels.cs]]
- [[Components/Charts/CompareXGrid/CompareXGridModels.cs.md|CompareXGridModels.cs]]
- [[Components/Charts/TxFold/TxFold.razor.md|TxFold.razor]]
- [[Components/Charts/XGrid/Display/XGrid.razor.md|XGrid.razor]]
- [[Models/BarAggregationKind.cs.md|BarAggregationKind.cs]]
