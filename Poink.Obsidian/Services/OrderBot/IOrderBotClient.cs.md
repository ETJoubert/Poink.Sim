---
tags: [interface, feature/orderbot, kind/interface]
role: interface
feature: OrderBot
feature_source: folder
kind: interface
code_lines: 6
types: 1
methods: 2
fan_in: 4
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Services/OrderBot/IOrderBotClient.cs
---
# IOrderBotClient.cs

HTTP transport to the cTrader OrderBot bridge. Never throws for transport failures; they come back as an unsuccessful result.

**Feature:** OrderBot · **Kind:** interface · **Code lines:** 6 · **Used by:** 4

## Types
- `IOrderBotClient` — interface, interface · 2 methods

## Uses
- [[Services/OrderBot/OrderBotModels.cs.md|OrderBotModels.cs]]
