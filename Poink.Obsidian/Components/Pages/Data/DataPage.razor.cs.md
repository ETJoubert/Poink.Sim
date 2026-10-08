---
tags: [code-behind]
source: Poink.Sim/Components/Pages/Data/DataPage.razor.cs
---
# DataPage.razor.cs

DaysMissing (in source, no DB row) / DaysMismatched (in both, counts differ -- likely a partial prior import) / DaysMatching (already up to date, safe to skip). NeedsImportDates is Missing union Mismatched -- what a diff-only import writes.

**Used by:** 1 file (see Backlinks)

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
