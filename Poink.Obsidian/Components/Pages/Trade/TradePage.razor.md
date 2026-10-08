---
tags: [page]
source: Poink.Sim/Components/Pages/Trade/TradePage.razor
---
# TradePage.razor

Lays out the Trade page's four panels plus the account menu and connects each to its own narrow driver interface. Cross-panel state (selected symbol, side-panel collapse, settings persistence) lives in the injected TradeCoordinator, not here.

**Used by:** 3 files (see Backlinks)

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
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
