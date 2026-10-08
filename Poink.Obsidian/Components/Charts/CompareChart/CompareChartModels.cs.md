---
tags: [model, feature/comparechart, kind/data-bundle]
role: model
feature: CompareChart
feature_source: folder
kind: data-bundle
code_lines: 19
types: 5
methods: 0
fan_in: 13
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Components/Charts/CompareChart/CompareChartModels.cs
---
# CompareChartModels.cs

How CompareChart's main pane scales its series.

**Feature:** CompareChart · **Kind:** bundle of data types · **Code lines:** 19 · **Used by:** 13

## Types
- `CompareChartScaleMode` — enum, enum · 2 values
- `CompareChartSeriesStyle` — enum, enum · 2 values
- `CompareChartZone` — record, data type (at most one helper method) · 4 properties
- `CompareChartMarker` — record, data type (at most one helper method) · 3 properties
- `CompareChartSeries` — record, data type (at most one helper method) · 5 properties

## Uses
- [[Components/Charts/MarketChart/Display/MarketChart.razor.cs.md|MarketChart.razor.cs]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
