---
tags: [code-behind]
source: Poink.Sim/Components/Pages/Trade/TradePage.razor.cs
---
# TradePage.razor.cs

Lays out the Trade page's four panels plus the account menu and connects each to its own narrow driver interface. Cross-panel state (selected symbol, side-panel collapse, settings persistence) lives in the injected TradeCoordinator, not here.

**Used by:** 0 files (see Backlinks)

## Uses
- [[Components/Charts/XGrid/Helper/XGridStats.cs.md|XGridStats.cs]]
- [[Components/Pages/Trade/Components/TradeChartPanel/TradeChartPanel.razor.md|TradeChartPanel.razor]]
- [[Components/Pages/Trade/Helpers/LiveTradeStatisticsHelper.cs.md|LiveTradeStatisticsHelper.cs]]
- [[Components/Pages/Trade/Helpers/PositionCloseHelper.cs.md|PositionCloseHelper.cs]]
- [[Components/Pages/Trade/Helpers/PositionPnlHelper.cs.md|PositionPnlHelper.cs]]
- [[Components/Pages/Trade/Helpers/PositionRiskHelper.cs.md|PositionRiskHelper.cs]]
- [[Components/Pages/Trade/Helpers/QuickTradeHelper.cs.md|QuickTradeHelper.cs]]
- [[Components/Pages/Trade/ITradeCoordinator.cs.md|ITradeCoordinator.cs]]
- [[Components/Pages/Trade/ITradeDraftService.cs.md|ITradeDraftService.cs]]
- [[Components/Pages/Trade/Models/PositionCloseModels.cs.md|PositionCloseModels.cs]]
- [[Components/Pages/Trade/Models/QuickTradeModels.cs.md|QuickTradeModels.cs]]
- [[Components/Pages/Trade/Services/IPositionCloseService.cs.md|IPositionCloseService.cs]]
- [[Components/Pages/Trade/TradeDraftModels.cs.md|TradeDraftModels.cs]]
- [[Components/Pages/Trade/TradePage.razor.md|TradePage.razor]]
- [[Components/Shared/FinancialsPanel/FinancialsPanel.razor.md|FinancialsPanel.razor]]
- [[Components/Shared/PositionTable/PositionRowFormat.cs.md|PositionRowFormat.cs]]
- [[Components/Shared/PositionTable/PositionTableModels.cs.md|PositionTableModels.cs]]
- [[Components/Shared/TradeConsole/TradeConsole.razor.md|TradeConsole.razor]]
- [[Components/Shared/TradeConsole/TradeConsoleModels.cs.md|TradeConsoleModels.cs]]
- [[Helpers/ILocalStorageHelper.cs.md|ILocalStorageHelper.cs]]
- [[Helpers/ISessionStorageHelper.cs.md|ISessionStorageHelper.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
- [[Models/StatGroup.cs.md|StatGroup.cs]]
- [[Models/SymbolMetadataModels.cs.md|SymbolMetadataModels.cs]]
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
- [[Services/CTrader/IAccountAccess.cs.md|IAccountAccess.cs]]
- [[Services/CTrader/ITickStreamAccess.cs.md|ITickStreamAccess.cs]]
- [[Services/CTrader/ITradeExecutionAccess.cs.md|ITradeExecutionAccess.cs]]
- [[Services/CTrader/ITradeHistoryService.cs.md|ITradeHistoryService.cs]]
- [[Services/MarketData/Recording/ILiveTickRecorder.cs.md|ILiveTickRecorder.cs]]
- [[Services/Symbols/ISymbolMetadataCache.cs.md|ISymbolMetadataCache.cs]]
- [[Services/Trader/Risk/RiskHelper.cs.md|RiskHelper.cs]]
