---
tags: [interface, feature/analize, kind/mixed]
role: interface
feature: Analize
feature_source: usage
kind: mixed
code_lines: 38
types: 5
methods: 5
fan_in: 3
fan_out: 2
summary_source: generated
candidates: []
source: Poink.Sim/Repository/Interfaces/IAnalysisHistoryRepository.cs
---
# IAnalysisHistoryRepository.cs

Contract for the analysis history repository.

**Feature:** Analize (by usage) · **Kind:** mixed types · **Code lines:** 38 · **Used by:** 3

## Types
- `AnalysisHistorySeriesSummary` — record, data type (at most one helper method) · 5 properties
- `AnalysisHistoryRecord` — record, data type (at most one helper method) · 23 properties
- `AnalysisHistoryBin` — record, data type (at most one helper method) · 5 properties
- `AnalysisHistoryDetail` — record, data type (at most one helper method) · 2 properties
- `IAnalysisHistoryRepository` — interface, interface · 5 methods

## Uses
- [[Models/PriceReturnModels.cs.md|PriceReturnModels.cs]]
- [[Models/TimeAboveBelowModels.cs.md|TimeAboveBelowModels.cs]]
