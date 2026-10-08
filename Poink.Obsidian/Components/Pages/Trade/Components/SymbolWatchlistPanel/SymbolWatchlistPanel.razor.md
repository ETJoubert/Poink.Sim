---
tags: [component, feature/trade, kind/component]
role: component
feature: Trade
feature_source: folder
kind: component
code_lines: 118
types: 1
methods: 0
fan_in: 3
fan_out: 6
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/Components/SymbolWatchlistPanel/SymbolWatchlistPanel.razor
---
# SymbolWatchlistPanel.razor

Left sidebar of the live Trade page -- symbol search/category/stored-data filters and the streaming watchlist. Owns its own filter/tab UI state; watch/unwatch, symbol category and tick coverage loading go through the injected session, symbol and tick services directly.

**Feature:** Trade · **Kind:** Razor component · **Code lines:** 118 · **Used by:** 3

## Types
- `SymbolWatchlistPanel` — component, Razor component · empty

## Uses
- [[Components/Pages/Trade/Components/SymbolCoverageBadge/SymbolCoverageBadge.razor.md|SymbolCoverageBadge.razor]]
- [[Components/Pages/Trade/Components/SymbolWatchlistPanel/SymbolWatchlistPanel.razor.cs.md|SymbolWatchlistPanel.razor.cs]]
- [[Components/Pages/Trade/Helpers/SymbolCoverageHelper.cs.md|SymbolCoverageHelper.cs]]
- [[Components/Shared/Icon/Icon.razor.md|Icon.razor]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Models/SymbolModels.cs.md|SymbolModels.cs]]
