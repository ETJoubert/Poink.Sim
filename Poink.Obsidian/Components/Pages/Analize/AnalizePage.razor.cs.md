---
tags: [code-behind, feature/analize, kind/component-code]
role: code-behind
feature: Analize
feature_source: folder
kind: component-code
code_lines: 620
types: 11
methods: 30
fan_in: 3
fan_out: 18
summary_source: generated
candidates: []
source: Poink.Sim/Components/Pages/Analize/AnalizePage.razor.cs
---
# AnalizePage.razor.cs

Code-behind logic for the AnalizePage component.

**Feature:** Analize · **Kind:** component code-behind · **Code lines:** 620 · **Used by:** 3

## Types
- `AnalizeBinRow` — record, data type (at most one helper method) · 3 properties
- `BarrierResultRow` — record, data type (at most one helper method) · 14 properties
- `ActiveAnalysis` — enum, enum · 3 values
- `AnalysisSeriesSummaryView` — record, data type (at most one helper method) · 5 properties
- `AnalysisDetailView` — record, data type (at most one helper method) · 18 properties
- `AnalizePage` — class, Razor component · 30 methods, 25 properties, 48 fields
- `AnalizeMainTab` — enum, nested, enum · 3 values
- `AnalizeLeftTab` — enum, nested, enum · 3 values
- `TimeAboveBelowView` — record, nested, data type (at most one helper method) · 3 properties
- `ReturnView` — record, nested, data type (at most one helper method) · 3 properties
- `SessionEntry` — record, nested, data type (at most one helper method) · 10 properties

## Uses
- [[Components/Charts/HistogramChart/HistogramChart.razor.cs.md|HistogramChart.razor.cs]]
- [[Components/Charts/MarketChart/Display/MarketChart.razor.cs.md|MarketChart.razor.cs]]
- [[Components/Pages/Analize/AnalizePage.razor.md|AnalizePage.razor]]
- [[Models/BarAggregationKind.cs.md|BarAggregationKind.cs]]
- [[Models/BarrierMapModels.cs.md|BarrierMapModels.cs]]
- [[Models/PriceReturnModels.cs.md|PriceReturnModels.cs]]
- [[Models/TimeAboveBelowModels.cs.md|TimeAboveBelowModels.cs]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
- [[Repository/AnalysisHistoryRepository.cs.md|AnalysisHistoryRepository.cs]]
- [[Repository/Interfaces/IAnalysisHistoryRepository.cs.md|IAnalysisHistoryRepository.cs]]
- [[Services/Analysis/BarrierMapCsv.cs.md|BarrierMapCsv.cs]]
- [[Services/Analysis/Interfaces/IBarrierMapAnalysisService.cs.md|IBarrierMapAnalysisService.cs]]
- [[Services/Analysis/Interfaces/IPriceReturnAnalysisService.cs.md|IPriceReturnAnalysisService.cs]]
- [[Services/Analysis/Interfaces/ITimeAboveBelowAnalysisService.cs.md|ITimeAboveBelowAnalysisService.cs]]
- [[Services/Charts/BarKindChartService.cs.md|BarKindChartService.cs]]
- [[Services/Symbols/ISymbolTableResolver.cs.md|ISymbolTableResolver.cs]]
- [[Services/Symbols/SymbolTableResolver.cs.md|SymbolTableResolver.cs]]
- [[wwwroot/js/fileDownload.js.md|fileDownload.js]]
