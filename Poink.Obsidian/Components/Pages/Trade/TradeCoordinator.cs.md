---
tags: [class, feature/trade, kind/behavior]
role: class
feature: Trade
feature_source: folder
kind: behavior
code_lines: 132
types: 1
methods: 14
fan_in: 1
fan_out: 10
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/TradeCoordinator.cs
---
# TradeCoordinator.cs

Scoped, so it's shared across TradePage and its panels for one circuit without being a component itself. Changed mirrors the same background-thread-notification pattern Changed already uses -- subscribers marshal onto their own render context themselves (see TradePage.HandleCoordinatorChanged).

**Feature:** Trade · **Kind:** class with behaviour · **Code lines:** 132 · **Used by:** 1

## Types
- `TradeCoordinator` — class, class with behaviour · 14 methods, 7 properties, 13 fields, 1 constructor, 1 event

## Uses
- [[Components/Pages/Trade/Components/TradeChartPanel/ChartPanelSettings.cs.md|ChartPanelSettings.cs]]
- [[Components/Pages/Trade/Helpers/TradeChartSettingsHelper.cs.md|TradeChartSettingsHelper.cs]]
- [[Components/Pages/Trade/ITradeCoordinator.cs.md|ITradeCoordinator.cs]]
- [[Helpers/ILocalStorageHelper.cs.md|ILocalStorageHelper.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Models/TradePageSettings.cs.md|TradePageSettings.cs]]
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
- [[Services/CTrader/ITickStreamAccess.cs.md|ITickStreamAccess.cs]]
- [[Services/CTrader/IWatchlistAccess.cs.md|IWatchlistAccess.cs]]
- [[Services/Symbols/ISymbolMetadataCache.cs.md|ISymbolMetadataCache.cs]]
