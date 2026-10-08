---
tags: [model, feature/profiler, kind/behavior]
role: model
feature: Profiler
feature_source: folder
kind: behavior
code_lines: 76
types: 1
methods: 7
fan_in: 5
fan_out: 11
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Profiler/Models/ProfilerSymbolData.cs
---
# ProfilerSymbolData.cs

Everything loaded for one Profiler symbol: its bars, quartile bars and per-bar price histograms, accumulated chunk by chunk as older history backfills, plus the chart-row projection for the active bar kind.

**Feature:** Profiler · **Kind:** class with behaviour · **Code lines:** 76 · **Used by:** 5

## Types
- `ProfilerSymbolData` — class, class with behaviour · 7 methods, 18 properties, 3 fields, 1 constructor

## Uses
- [[Components/Charts/MarketChart/Display/MarketChart.razor.cs.md|MarketChart.razor.cs]]
- [[Components/Charts/XGrid/Display/XGrid.razor.md|XGrid.razor]]
- [[Components/Charts/XGrid/Helper/XGridStats.cs.md|XGridStats.cs]]
- [[Components/Pages/Profiler/Helpers/ProfilerSymbolStatsHelper.cs.md|ProfilerSymbolStatsHelper.cs]]
- [[Helpers/ChartBarRowHelper.cs.md|ChartBarRowHelper.cs]]
- [[Models/BarAggregationKind.cs.md|BarAggregationKind.cs]]
- [[Models/BarPriceHistogram.cs.md|BarPriceHistogram.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Models/ProfilerModels.cs.md|ProfilerModels.cs]]
- [[Models/QuartileBar.cs.md|QuartileBar.cs]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
