---
tags: [model, feature/shared, kind/data-bundle]
role: model
feature: Shared
feature_source: shared
kind: data-bundle
code_lines: 33
types: 3
methods: 0
fan_in: 16
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Models/SymbolModels.cs
---
# SymbolModels.cs

Trade page symbol-search filter bucket, resolved from a cTrader symbol category's name via SymbolCategoryClassifier.

**Feature:** Shared · **Kind:** bundle of data types · **Code lines:** 33 · **Used by:** 16

## Types
- `SymbolModel` — record, data type (at most one helper method) · 11 properties
- `SymbolCategoryBucket` — enum, enum · 7 values
- `SymbolCategoryModel` — record, data type (at most one helper method) · 8 properties

## Uses
- [[Components/Charts/TickTimeProfileChart/TickTimeProfileChart.razor.cs.md|TickTimeProfileChart.razor.cs]]
- [[Components/Charts/TickVolumeProfileChart/TickVolumeProfileChart.razor.cs.md|TickVolumeProfileChart.razor.cs]]
