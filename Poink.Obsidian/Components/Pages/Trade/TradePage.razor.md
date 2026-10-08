---
tags: [page, feature/trade, kind/page]
role: page
feature: Trade
feature_source: folder
kind: page
code_lines: 158
types: 1
methods: 0
fan_in: 2
fan_out: 13
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/TradePage.razor
---
# TradePage.razor

Lays out the Trade page's four panels plus the account menu and connects each to its own narrow driver interface. Cross-panel state (selected symbol, side-panel collapse, settings persistence) lives in the injected TradeCoordinator, not here.

**Feature:** Trade · **Kind:** Razor page · **Code lines:** 158 · **Used by:** 2

## Types
- `TradePage` — component, Razor component · empty

## Uses
- [[Components/Pages/Trade/Components/QuickTradeConsole/QuickTradeConsole.razor.md|QuickTradeConsole.razor]]
- [[Components/Pages/Trade/Components/SymbolWatchlistPanel/SymbolWatchlistPanel.razor.md|SymbolWatchlistPanel.razor]]
- [[Components/Pages/Trade/Components/TradeAccountMenu/TradeAccountMenu.razor.md|TradeAccountMenu.razor]]
- [[Components/Pages/Trade/Components/TradeChartPanel/TradeChartPanel.razor.md|TradeChartPanel.razor]]
- [[Components/Pages/Trade/Models/QuickTradeModels.cs.md|QuickTradeModels.cs]]
- [[Components/Shared/FinancialsPanel/FinancialsPanel.razor.md|FinancialsPanel.razor]]
- [[Components/Shared/Icon/Icon.razor.md|Icon.razor]]
- [[Components/Shared/PositionTable/PositionTable.razor.md|PositionTable.razor]]
- [[Components/Shared/StatisticsPanel/StatisticsPanel.razor.md|StatisticsPanel.razor]]
- [[Components/Shared/TradeConsole/TradeConsole.razor.md|TradeConsole.razor]]
- [[Components/Shared/TraderSidePanel/TraderSidePanel.razor.md|TraderSidePanel.razor]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
