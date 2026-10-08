---
tags: [helper, feature/orderbot, kind/single-method, candidate/single-method]
role: helper
feature: OrderBot
feature_source: folder
kind: single-method
code_lines: 14
types: 1
methods: 1
fan_in: 2
fan_out: 3
summary_source: documented
candidates: [single-method]
source: Poink.Sim/Services/OrderBot/OrderBotRoutingHelper.cs
---
# OrderBotRoutingHelper.cs

Decides whether the selected account's trades belong to the OrderBot bridge rather than the Open API.

**Feature:** OrderBot · **Kind:** single-method class · **Code lines:** 14 · **Used by:** 2

## Types
- `OrderBotRoutingHelper` — class, single-method class · 1 method

## Candidates
- **single-method** — Fold `TryGetBotAccount` into a sibling helper (`OrderBotStatusHelper.cs`).

## Uses
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
- [[Services/CTrader/IAccountAccess.cs.md|IAccountAccess.cs]]
- [[Services/OrderBot/OrderBotOptions.cs.md|OrderBotOptions.cs]]
