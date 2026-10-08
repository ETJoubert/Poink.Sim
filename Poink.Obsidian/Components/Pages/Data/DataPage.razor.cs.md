---
tags: [code-behind, feature/data, kind/component-code]
role: code-behind
feature: Data
feature_source: folder
kind: component-code
code_lines: 330
types: 4
methods: 34
fan_in: 1
fan_out: 14
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Data/DataPage.razor.cs
---
# DataPage.razor.cs

DaysMissing (in source, no DB row) / DaysMismatched (in both, counts differ -- likely a partial prior import) / DaysMatching (already up to date, safe to skip). NeedsImportDates is Missing union Mismatched -- what a diff-only import writes.

**Feature:** Data · **Kind:** component code-behind · **Code lines:** 330 · **Used by:** 1

## Types
- `DataPage` — class, Razor component · 34 methods, 11 properties, 22 fields
- `Tab` — enum, nested, enum · 4 values
- `SymbolRow` — record, nested, data type (at most one helper method) · 10 properties
- `ImportDiffSummary` — record, nested, data type (at most one helper method) · 5 properties

## Uses
- [[Components/Pages/Data/DataPage.razor.md|DataPage.razor]]
- [[Data/Entities/Symbol.cs.md|Symbol.cs]]
- [[Models/SymbolMetadataModels.cs.md|SymbolMetadataModels.cs]]
- [[Models/SymbolModels.cs.md|SymbolModels.cs]]
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
- [[Services/CTrader/ICTraderSessionService.cs.md|ICTraderSessionService.cs]]
- [[Services/MarketData/Import/IZTicksImportService.cs.md|IZTicksImportService.cs]]
- [[Services/MarketData/Import/ZTicksImportModels.cs.md|ZTicksImportModels.cs]]
- [[Services/MarketData/ITickIngest.cs.md|ITickIngest.cs]]
- [[Services/MarketData/MarketDataModels.cs.md|MarketDataModels.cs]]
- [[Services/MarketData/TickTableNaming.cs.md|TickTableNaming.cs]]
- [[Services/Symbols/ISymbolMetadataCache.cs.md|ISymbolMetadataCache.cs]]
- [[Services/Symbols/ISymbolService.cs.md|ISymbolService.cs]]
- [[Services/Symbols/SymbolService.cs.md|SymbolService.cs]]
