---
tags: [code-behind, feature/profiler, kind/component-code]
role: code-behind
feature: Profiler
feature_source: folder
kind: component-code
code_lines: 624
types: 3
methods: 53
fan_in: 1
fan_out: 27
summary_source: generated
candidates: []
source: Poink.Sim/Components/Pages/Profiler/ProfilerPage.razor.cs
---
# ProfilerPage.razor.cs

Code-behind logic for the ProfilerPage component.

**Feature:** Profiler · **Kind:** component code-behind · **Code lines:** 624 · **Used by:** 1

## Types
- `ProfilerPage` — class, Razor component · 53 methods, 15 properties, 55 fields
- `PanelTab` — enum, nested, enum · 3 values
- `Drawing` — record, nested, data type (at most one helper method) · 12 properties

## Uses
- [[Components/Charts/CompareChart/CompareChartModels.cs.md|CompareChartModels.cs]]
- [[Components/Charts/MarketChart/Display/MarketChart.razor.cs.md|MarketChart.razor.cs]]
- [[Components/Pages/Profiler/Models/ProfilerChartSetModels.cs.md|ProfilerChartSetModels.cs]]
- [[Components/Pages/Profiler/Models/ProfilerCompareModels.cs.md|ProfilerCompareModels.cs]]
- [[Components/Pages/Profiler/Models/ProfilerPageSettingsModels.cs.md|ProfilerPageSettingsModels.cs]]
- [[Components/Pages/Profiler/Models/ProfilerSymbolData.cs.md|ProfilerSymbolData.cs]]
- [[Components/Pages/Profiler/ProfilerPage.razor.md|ProfilerPage.razor]]
- [[Components/Shared/Indicators/Indicators.razor.md|Indicators.razor]]
- [[Components/Shared/Indicators/IndicatorsModels.cs.md|IndicatorsModels.cs]]
- [[Helpers/ChartBarRowHelper.cs.md|ChartBarRowHelper.cs]]
- [[Helpers/ILocalStorageHelper.cs.md|ILocalStorageHelper.cs]]
- [[Helpers/SymbolColorHelper.cs.md|SymbolColorHelper.cs]]
- [[Helpers/TickTableRangeHelper.cs.md|TickTableRangeHelper.cs]]
- [[Models/BarAggregationKind.cs.md|BarAggregationKind.cs]]
- [[Models/HlocAvdtBar.cs.md|HlocAvdtBar.cs]]
- [[Models/ProfilerModels.cs.md|ProfilerModels.cs]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Repository/Interfaces/ITRangeChartRepository.cs.md|ITRangeChartRepository.cs]]
- [[Repository/Interfaces/ITRangeReferenceChartRepository.cs.md|ITRangeReferenceChartRepository.cs]]
- [[Repository/TickRepository.cs.md|TickRepository.cs]]
- [[Repository/TRangeChartRepository.cs.md|TRangeChartRepository.cs]]
- [[Repository/TRangeReferenceChartRepository.cs.md|TRangeReferenceChartRepository.cs]]
- [[Services/Profiler/IProfilerService.cs.md|IProfilerService.cs]]
- [[Services/Simulation/SimulationMarketFeed.cs.md|SimulationMarketFeed.cs]]
- [[Services/Symbols/ISymbolTableResolver.cs.md|ISymbolTableResolver.cs]]
- [[Services/Symbols/SymbolTableResolver.cs.md|SymbolTableResolver.cs]]
