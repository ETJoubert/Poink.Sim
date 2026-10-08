---
tags: [code-behind, feature/histogramchart, kind/component-code]
role: code-behind
feature: HistogramChart
feature_source: folder
kind: component-code
code_lines: 38
types: 3
methods: 6
fan_in: 2
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/HistogramChart/HistogramChart.razor.cs
---
# HistogramChart.razor.cs

Public because a [Parameter] can't expose a less-accessible type; callers convert from their own internal bin model.

**Feature:** HistogramChart · **Kind:** component code-behind · **Code lines:** 38 · **Used by:** 2

## Types
- `HistogramBar` — record struct, data type (at most one helper method) · 3 properties
- `HistogramSeries` — record struct, data type (at most one helper method) · 3 properties
- `HistogramChart` — class, Razor component · 6 methods, 12 properties

## Uses
- [[Components/Charts/HistogramChart/HistogramChart.razor.md|HistogramChart.razor]]
