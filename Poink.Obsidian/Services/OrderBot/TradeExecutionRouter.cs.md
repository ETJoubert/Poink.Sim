---
tags: [class, feature/orderbot, kind/behavior]
role: class
feature: OrderBot
feature_source: folder
kind: behavior
code_lines: 78
types: 1
methods: 5
fan_in: 1
fan_out: 7
summary_source: documented
candidates: []
source: Poink.Sim/Services/OrderBot/TradeExecutionRouter.cs
---
# TradeExecutionRouter.cs

The app-wide ITradeExecutionAccess: a live account's trades go to the cTrader OrderBot bridge (the broker blocks Open API trading on live), everything else to the Open API session unchanged.

**Feature:** OrderBot · **Kind:** class with behaviour · **Code lines:** 78 · **Used by:** 1

## Types
- `TradeExecutionRouter` — class, class with behaviour · 5 methods, 1 property, 6 fields, 1 constructor

## Uses
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
- [[Services/CTrader/IAccountAccess.cs.md|IAccountAccess.cs]]
- [[Services/CTrader/ITradeExecutionAccess.cs.md|ITradeExecutionAccess.cs]]
- [[Services/CTrader/IWatchlistAccess.cs.md|IWatchlistAccess.cs]]
- [[Services/OrderBot/IOrderBotTradeExecution.cs.md|IOrderBotTradeExecution.cs]]
- [[Services/OrderBot/OrderBotOptions.cs.md|OrderBotOptions.cs]]
- [[Services/OrderBot/OrderBotRoutingHelper.cs.md|OrderBotRoutingHelper.cs]]
