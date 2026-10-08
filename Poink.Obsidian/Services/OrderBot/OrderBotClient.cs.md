---
tags: [class, feature/orderbot, kind/behavior]
role: class
feature: OrderBot
feature_source: folder
kind: behavior
code_lines: 96
types: 1
methods: 3
fan_in: 1
fan_out: 3
summary_source: interface-doc
candidates: []
source: Poink.Sim/Services/OrderBot/OrderBotClient.cs
---
# OrderBotClient.cs

HTTP transport to the cTrader OrderBot bridge. Never throws for transport failures; they come back as an unsuccessful result.

**Feature:** OrderBot · **Kind:** class with behaviour · **Code lines:** 96 · **Used by:** 1

## Types
- `OrderBotClient` — class, class with behaviour · 3 methods, 1 property, 8 fields, 1 constructor

## Uses
- [[Services/OrderBot/IOrderBotClient.cs.md|IOrderBotClient.cs]]
- [[Services/OrderBot/OrderBotModels.cs.md|OrderBotModels.cs]]
- [[Services/OrderBot/OrderBotOptions.cs.md|OrderBotOptions.cs]]
