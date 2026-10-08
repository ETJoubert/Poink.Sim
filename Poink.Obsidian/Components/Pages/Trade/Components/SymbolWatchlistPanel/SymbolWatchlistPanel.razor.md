---
tags: [component]
source: Poink.Sim/Components/Pages/Trade/Components/SymbolWatchlistPanel/SymbolWatchlistPanel.razor
---
# SymbolWatchlistPanel.razor

Left sidebar of the live Trade page -- symbol search/category/stored-data filters and the streaming watchlist. Owns its own filter/tab UI state; watch/unwatch, symbol category and tick coverage loading go through the injected session, symbol and tick services directly.

**Used by:** 4 files (see Backlinks)

## Uses
- [[Components/Pages/Trade/Components/SymbolCoverageBadge/SymbolCoverageBadge.razor.md|SymbolCoverageBadge.razor]]
- [[Components/Pages/Trade/Components/SymbolWatchlistPanel/SymbolWatchlistPanel.razor.cs.md|SymbolWatchlistPanel.razor.cs]]
- [[Components/Pages/Trade/Helpers/SymbolCoverageHelper.cs.md|SymbolCoverageHelper.cs]]
- [[Components/Shared/Icon/Icon.razor.md|Icon.razor]]
- [[Models/SymbolModels.cs.md|SymbolModels.cs]]
