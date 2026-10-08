---
tags: [helper, feature/trader, kind/static-helper]
role: helper
feature: Trader
feature_source: folder
kind: static-helper
code_lines: 142
types: 1
methods: 23
fan_in: 14
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Services/Trader/Risk/RiskHelper.cs
---
# RiskHelper.cs

Stateless risk calculations shared by simulation and training. Position sizing mirrors Trader: committed = fraction * cash, notional = committed * leverage, size = notional / price. Commission is excluded.

**Feature:** Trader · **Kind:** static helper · **Code lines:** 142 · **Used by:** 14

## Types
- `RiskHelper` — class, static helper · 23 methods

## Uses
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Services/Trader/Trader.cs.md|Trader.cs]]
