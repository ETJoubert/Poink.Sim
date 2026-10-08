---
tags: [helper, feature/orderbot, kind/single-method, candidate/single-method]
role: helper
feature: OrderBot
feature_source: folder
kind: single-method
code_lines: 20
types: 1
methods: 1
fan_in: 1
fan_out: 2
summary_source: documented
candidates: [single-method]
source: Poink.Sim/Services/OrderBot/OrderBotStatusHelper.cs
---
# OrderBotStatusHelper.cs

Turns a health probe into the bridge status shown on the Trade page for the selected live account.

**Feature:** OrderBot · **Kind:** single-method class · **Code lines:** 20 · **Used by:** 1

## Types
- `OrderBotStatusHelper` — class, single-method class · 1 method

## Candidates
- **single-method** — Inline `Evaluate` into its only user `OrderBotStatusMonitor.cs`.

## Uses
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
- [[Services/OrderBot/OrderBotModels.cs.md|OrderBotModels.cs]]
