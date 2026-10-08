---
tags: [helper, feature/trade, kind/static-helper]
role: helper
feature: Trade
feature_source: folder
kind: static-helper
code_lines: 41
types: 1
methods: 7
fan_in: 4
fan_out: 3
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/Helpers/SymbolCoverageHelper.cs
---
# SymbolCoverageHelper.cs

Maps cTrader symbols to their stored tick history. Coverage is judged on the live table because Trade page history before today always reads it; "current" means it reaches the last weekday before today, the newest day the Data page import can supply.

**Feature:** Trade · **Kind:** static helper · **Code lines:** 41 · **Used by:** 4

## Types
- `SymbolCoverageHelper` — class, static helper · 7 methods

## Uses
- [[Components/Pages/Trade/Models/SymbolCoverageModels.cs.md|SymbolCoverageModels.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Services/MarketData/TickTableNaming.cs.md|TickTableNaming.cs]]
