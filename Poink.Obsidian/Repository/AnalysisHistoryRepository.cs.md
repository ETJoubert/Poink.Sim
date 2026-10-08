---
tags: [repository, feature/analize, kind/behavior]
role: repository
feature: Analize
feature_source: usage
kind: behavior
code_lines: 228
types: 3
methods: 9
fan_in: 2
fan_out: 3
summary_source: generated
candidates: []
source: Poink.Sim/Repository/AnalysisHistoryRepository.cs
---
# AnalysisHistoryRepository.cs

Repository that persists and loads analysis history.

**Feature:** Analize (by usage) · **Kind:** class with behaviour · **Code lines:** 228 · **Used by:** 2

## Types
- `AnalysisHistoryRepository` — class, class with behaviour · 9 methods, 6 fields, 1 constructor
- `DetailRow` — record, nested, data type (at most one helper method) · 23 properties
- `Sql` — class, nested, static constants · 12 fields

## Uses
- [[Models/PriceReturnModels.cs.md|PriceReturnModels.cs]]
- [[Models/TimeAboveBelowModels.cs.md|TimeAboveBelowModels.cs]]
- [[Repository/Interfaces/IAnalysisHistoryRepository.cs.md|IAnalysisHistoryRepository.cs]]
