---
tags: [code-behind]
source: Poink.Sim/Components/Pages/Trade/Components/SymbolWatchlistPanel/SymbolWatchlistPanel.razor.cs
---
# SymbolWatchlistPanel.razor.cs

Left sidebar of the live Trade page -- symbol search/category/stored-data filters and the streaming watchlist. Owns its own filter/tab UI state; watch/unwatch, symbol category and tick coverage loading go through the injected session, symbol and tick services directly.

**Used by:** 1 file (see Backlinks)

## Uses
- [[Components/Charts/TickTimeProfileChart/TickTimeProfileChart.razor.cs.md|TickTimeProfileChart.razor.cs]]
- [[Components/Charts/TickVolumeProfileChart/TickVolumeProfileChart.razor.cs.md|TickVolumeProfileChart.razor.cs]]
- [[Components/Pages/Trade/Components/SymbolWatchlistPanel/SymbolWatchlistPanel.razor.md|SymbolWatchlistPanel.razor]]
- [[Components/Pages/Trade/Helpers/SymbolCoverageHelper.cs.md|SymbolCoverageHelper.cs]]
- [[Components/Pages/Trade/Helpers/SymbolFilterHelper.cs.md|SymbolFilterHelper.cs]]
- [[Components/Pages/Trade/Models/SymbolCoverageModels.cs.md|SymbolCoverageModels.cs]]
- [[Components/Pages/Trade/Models/SymbolFilterModels.cs.md|SymbolFilterModels.cs]]
- [[Helpers/SymbolColorHelper.cs.md|SymbolColorHelper.cs]]
- [[Models/SymbolModels.cs.md|SymbolModels.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Repository/TickRepository.cs.md|TickRepository.cs]]
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
- [[Services/CTrader/IWatchlistAccess.cs.md|IWatchlistAccess.cs]]
- [[Services/Symbols/ISymbolMetadataCache.cs.md|ISymbolMetadataCache.cs]]
- [[Services/Symbols/ISymbolService.cs.md|ISymbolService.cs]]
- [[Services/Symbols/SymbolService.cs.md|SymbolService.cs]]
