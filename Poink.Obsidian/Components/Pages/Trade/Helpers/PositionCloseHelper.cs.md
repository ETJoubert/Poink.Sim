---
tags: [helper, feature/trade, kind/static-helper]
role: helper
feature: Trade
feature_source: folder
kind: static-helper
code_lines: 36
types: 1
methods: 7
fan_in: 5
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/Helpers/PositionCloseHelper.cs
---
# PositionCloseHelper.cs

Decides which positions the close helpers (breakeven watch, auto-arm, time stop) should close now.

**Feature:** Trade · **Kind:** static helper · **Code lines:** 36 · **Used by:** 5

## Types
- `PositionCloseHelper` — class, static helper · 7 methods, 3 fields

## Uses
- [[Components/Pages/Trade/Models/PositionCloseModels.cs.md|PositionCloseModels.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
