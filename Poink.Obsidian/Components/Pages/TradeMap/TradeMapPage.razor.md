---
tags: [page, feature/trademap, kind/page]
role: page
feature: TradeMap
feature_source: folder
kind: page
code_lines: 59
types: 1
methods: 0
fan_in: 2
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/TradeMap/TradeMapPage.razor
---
# TradeMapPage.razor

Hand-authored architecture map of the live Trade page (/trade), not derived from reflection like Code/Arch/Calls -- every node, port and connector below is placed and named by hand against the actual TradePage/SymbolWatchlistPanel/TradeChartPanel/FinancialsPanel/PositionTable/TradeConsole source.

**Feature:** TradeMap · **Kind:** Razor page · **Code lines:** 59 · **Used by:** 2

## Types
- `TradeMapPage` — component, Razor component · empty

## Uses
- [[Components/Pages/TradeMap/Components/TradeMapCanvas/TradeMapCanvas.razor.md|TradeMapCanvas.razor]]
- [[Components/Pages/TradeMap/Components/TradeMapDetailDrawer/TradeMapDetailDrawer.razor.md|TradeMapDetailDrawer.razor]]
