---
tags: [class, feature/symbols, kind/static-helper]
role: class
feature: Symbols
feature_source: folder
kind: static-helper
code_lines: 20
types: 1
methods: 4
fan_in: 1
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Services/Symbols/SymbolCategoryClassifier.cs
---
# SymbolCategoryClassifier.cs

Maps a cTrader symbol category's name (plus its asset class's name) to one of the Trade page's filter buckets. Broker category names aren't standardized, so these are a first-draft guess -- check them against SymbolService.RefreshCategoriesFromCTraderAsync's log output and adjust here.

**Feature:** Symbols · **Kind:** static helper · **Code lines:** 20 · **Used by:** 1

## Types
- `SymbolCategoryClassifier` — class, static helper · 4 methods, 1 property

## Uses
- [[Components/Charts/TickTimeProfileChart/TickTimeProfileChart.razor.cs.md|TickTimeProfileChart.razor.cs]]
- [[Components/Charts/TickVolumeProfileChart/TickVolumeProfileChart.razor.cs.md|TickVolumeProfileChart.razor.cs]]
- [[Models/SymbolModels.cs.md|SymbolModels.cs]]
