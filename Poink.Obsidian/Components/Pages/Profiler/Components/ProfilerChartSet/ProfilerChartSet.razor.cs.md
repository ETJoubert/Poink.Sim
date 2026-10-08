---
tags: [code-behind, feature/profiler, kind/component-code]
role: code-behind
feature: Profiler
feature_source: folder
kind: component-code
code_lines: 212
types: 1
methods: 10
fan_in: 0
fan_out: 19
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Profiler/Components/ProfilerChartSet/ProfilerChartSet.razor.cs
---
# ProfilerChartSet.razor.cs

One Profiler symbol's full chart set: header, main chart, XGrid, volume/time profiles, subchart and TxFold, all driven by the page's global ProfilerChartSetOptions so every symbol is viewed the same way.

**Feature:** Profiler · **Kind:** component code-behind · **Code lines:** 212 · **Used by:** 0

## Types
- `ProfilerChartSet` — class, Razor component · 10 methods, 35 properties, 26 fields

## Uses
- [[Components/Charts/MarketChart/Display/MarketChart.razor.cs.md|MarketChart.razor.cs]]
- [[Components/Charts/TickTimeProfileChart/TickTimeProfileChart.razor.cs.md|TickTimeProfileChart.razor.cs]]
- [[Components/Charts/TickVolumeProfileChart/TickVolumeProfileChart.razor.cs.md|TickVolumeProfileChart.razor.cs]]
- [[Components/Charts/TxFold/Helper/TxFoldLevelOptions.cs.md|TxFoldLevelOptions.cs]]
- [[Components/Charts/XGrid/Display/XGrid.razor.md|XGrid.razor]]
- [[Components/Charts/XGrid/Helper/XGridStats.cs.md|XGridStats.cs]]
- [[Components/Pages/Profiler/Components/ProfilerChartSet/ProfilerChartSet.razor.md|ProfilerChartSet.razor]]
- [[Components/Pages/Profiler/Helpers/ProfilerSymbolStatsHelper.cs.md|ProfilerSymbolStatsHelper.cs]]
- [[Components/Pages/Profiler/Models/ProfilerChartSetModels.cs.md|ProfilerChartSetModels.cs]]
- [[Components/Pages/Profiler/Models/ProfilerSymbolData.cs.md|ProfilerSymbolData.cs]]
- [[Components/Shared/Indicators/IndicatorChartOverlayHelper.cs.md|IndicatorChartOverlayHelper.cs]]
- [[Components/Shared/Indicators/Indicators.razor.md|Indicators.razor]]
- [[Components/Shared/Indicators/IndicatorsHelper.cs.md|IndicatorsHelper.cs]]
- [[Helpers/ChartSetDividerScript.cs.md|ChartSetDividerScript.cs]]
- [[Helpers/SymbolColorHelper.cs.md|SymbolColorHelper.cs]]
- [[Models/BarAggregationKind.cs.md|BarAggregationKind.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
- [[Services/Charts/BarKindChartService.cs.md|BarKindChartService.cs]]
