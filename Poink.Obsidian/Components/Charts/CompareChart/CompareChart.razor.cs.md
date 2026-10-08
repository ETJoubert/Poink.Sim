---
tags: [code-behind, feature/comparechart, kind/component-code]
role: code-behind
feature: CompareChart
feature_source: folder
kind: component-code
code_lines: 114
types: 1
methods: 10
fan_in: 0
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/CompareChart/CompareChart.razor.cs
---
# CompareChart.razor.cs

Several series as lines on one shared time axis, for comparing symbols: a main pane scaled by ScaleMode, an optional lower pane for series with Pane 1, and a shaded window. Draws via its own compareChart.js module, not marketChart.js.

**Feature:** CompareChart · **Kind:** component code-behind · **Code lines:** 114 · **Used by:** 0

## Types
- `CompareChart` — class, Razor component · 10 methods, 11 properties, 12 fields

## Uses
- [[Components/Charts/CompareChart/CompareChart.razor.md|CompareChart.razor]]
- [[Components/Charts/CompareChart/CompareChartModels.cs.md|CompareChartModels.cs]]
- [[wwwroot/js/compareChart.js.md|compareChart.js]]
