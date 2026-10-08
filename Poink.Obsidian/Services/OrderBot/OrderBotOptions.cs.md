---
tags: [settings, feature/orderbot, kind/data, candidate/merge-data]
role: settings
feature: OrderBot
feature_source: folder
kind: data
code_lines: 9
types: 1
methods: 0
fan_in: 6
fan_out: 0
summary_source: documented
candidates: [merge-data]
source: Poink.Sim/Services/OrderBot/OrderBotOptions.cs
---
# OrderBotOptions.cs

Binds the "OrderBot" config section: where the cTrader OrderBot bridge listens and whether a live account's trades are routed to it instead of the Open API (which the broker blocks for live trading).

**Feature:** OrderBot · **Kind:** data type (at most one helper method) · **Code lines:** 9 · **Used by:** 6

## Types
- `OrderBotOptions` — class, data type (at most one helper method) · 6 properties, 1 field

## Candidates
- **merge-data** — Merge into `OrderBotModels.cs`.

## Uses
- _nothing in Poink.Sim_
