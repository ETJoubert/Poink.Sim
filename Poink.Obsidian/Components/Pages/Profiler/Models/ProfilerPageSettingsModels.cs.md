---
tags: [model, feature/profiler, kind/data]
role: model
feature: Profiler
feature_source: folder
kind: data
code_lines: 37
types: 1
methods: 0
fan_in: 1
fan_out: 7
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Profiler/Models/ProfilerPageSettingsModels.cs
---
# ProfilerPageSettingsModels.cs

Snapshot of the Profiler page's options (range, symbols, aggregation, panels, indicators, Lookback Window and chart set heights), persisted to browser localStorage so it survives reloading or navigating away and back.

**Feature:** Profiler · **Kind:** data type (at most one helper method) · **Code lines:** 37 · **Used by:** 1

## Types
- `ProfilerPageSettings` — record, data type (at most one helper method) · 28 properties, 1 field

## Uses
- [[Components/Charts/CompareChart/CompareChartModels.cs.md|CompareChartModels.cs]]
- [[Components/Charts/CompareTxFold/CompareTxFold.razor.md|CompareTxFold.razor]]
- [[Components/Charts/CompareXGrid/CompareXGrid.razor.md|CompareXGrid.razor]]
- [[Components/Pages/Profiler/Models/ProfilerCompareModels.cs.md|ProfilerCompareModels.cs]]
- [[Components/Shared/Indicators/Indicators.razor.md|Indicators.razor]]
- [[Components/Shared/Indicators/IndicatorsModels.cs.md|IndicatorsModels.cs]]
- [[Models/BarAggregationKind.cs.md|BarAggregationKind.cs]]
