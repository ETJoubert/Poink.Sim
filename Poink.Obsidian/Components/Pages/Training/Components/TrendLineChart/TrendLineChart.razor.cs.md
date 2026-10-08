---
tags: [code-behind, feature/training, kind/component-code]
role: code-behind
feature: Training
feature_source: folder
kind: component-code
code_lines: 95
types: 2
methods: 8
fan_in: 0
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Training/Components/TrendLineChart/TrendLineChart.razor.cs
---
# TrendLineChart.razor.cs

Generic rolling-window scatter chart for a per-episode statistic; callers supply only how to reduce a window of episodes to one number (Aggregate). Points only, no connecting line/fill, so trend is read from the point cloud's shape.

**Feature:** Training · **Kind:** component code-behind · **Code lines:** 95 · **Used by:** 0

## Types
- `TrendLineChart` — class, Razor component · 8 methods, 13 properties, 10 fields
- `ChartPoint` — record, nested, data type (at most one helper method) · 5 properties

## Uses
- [[Components/Pages/Training/Components/TrendLineChart/TrendLineChart.razor.md|TrendLineChart.razor]]
- [[Services/Training/RlEpisode.cs.md|RlEpisode.cs]]
