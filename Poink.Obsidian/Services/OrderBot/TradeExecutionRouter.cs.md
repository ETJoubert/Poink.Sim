---
tags: [class]
source: Poink.Sim/Services/OrderBot/TradeExecutionRouter.cs
---
# TradeExecutionRouter.cs

The app-wide ITradeExecutionAccess: a live account's trades go to the cTrader OrderBot bridge (the broker blocks Open API trading on live), everything else to the Open API session unchanged.

**Used by:** 1 file (see Backlinks)

## Uses
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
- [[Services/CTrader/IAccountAccess.cs.md|IAccountAccess.cs]]
- [[Services/CTrader/ITradeExecutionAccess.cs.md|ITradeExecutionAccess.cs]]
- [[Services/CTrader/IWatchlistAccess.cs.md|IWatchlistAccess.cs]]
- [[Services/OrderBot/IOrderBotTradeExecution.cs.md|IOrderBotTradeExecution.cs]]
- [[Services/OrderBot/OrderBotOptions.cs.md|OrderBotOptions.cs]]
- [[Services/OrderBot/OrderBotRoutingHelper.cs.md|OrderBotRoutingHelper.cs]]
