---
tags: [class]
source: Poink.Sim/Components/Pages/Trade/TradeCoordinator.cs
---
# TradeCoordinator.cs

Scoped, so it's shared across TradePage and its panels for one circuit without being a component itself. Changed mirrors the same background-thread-notification pattern Changed already uses -- subscribers marshal onto their own render context themselves (see TradePage.HandleCoordinatorChanged).

**Used by:** 2 files (see Backlinks)

## Uses
- [[Components/Pages/Trade/Components/TradeChartPanel/ChartPanelSettings.cs.md|ChartPanelSettings.cs]]
- [[Components/Pages/Trade/Helpers/TradeChartSettingsHelper.cs.md|TradeChartSettingsHelper.cs]]
- [[Components/Pages/Trade/ITradeCoordinator.cs.md|ITradeCoordinator.cs]]
- [[Helpers/ILocalStorageHelper.cs.md|ILocalStorageHelper.cs]]
- [[Models/TradePageSettings.cs.md|TradePageSettings.cs]]
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
- [[Services/CTrader/ITickStreamAccess.cs.md|ITickStreamAccess.cs]]
- [[Services/CTrader/IWatchlistAccess.cs.md|IWatchlistAccess.cs]]
- [[Services/Symbols/ISymbolMetadataCache.cs.md|ISymbolMetadataCache.cs]]
