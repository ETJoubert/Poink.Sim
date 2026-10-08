---
tags: [class, feature/orderbot, kind/behavior]
role: class
feature: OrderBot
feature_source: folder
kind: behavior
code_lines: 53
types: 1
methods: 2
fan_in: 1
fan_out: 7
summary_source: documented
candidates: []
source: Poink.Sim/Services/OrderBot/OrderBotStatusMonitor.cs
---
# OrderBotStatusMonitor.cs

Probes the OrderBot's health every poll interval while a live account is selected, logging only state transitions.

**Feature:** OrderBot · **Kind:** class with behaviour · **Code lines:** 53 · **Used by:** 1

## Types
- `OrderBotStatusMonitor` — class, class with behaviour · 2 methods, 1 property, 4 fields, 1 constructor, 1 event

## Uses
- [[Services/CTrader/IAccountAccess.cs.md|IAccountAccess.cs]]
- [[Services/OrderBot/IOrderBotClient.cs.md|IOrderBotClient.cs]]
- [[Services/OrderBot/IOrderBotStatus.cs.md|IOrderBotStatus.cs]]
- [[Services/OrderBot/OrderBotModels.cs.md|OrderBotModels.cs]]
- [[Services/OrderBot/OrderBotOptions.cs.md|OrderBotOptions.cs]]
- [[Services/OrderBot/OrderBotRoutingHelper.cs.md|OrderBotRoutingHelper.cs]]
- [[Services/OrderBot/OrderBotStatusHelper.cs.md|OrderBotStatusHelper.cs]]
