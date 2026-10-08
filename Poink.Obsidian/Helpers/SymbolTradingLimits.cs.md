---
tags: [class, feature/shared, kind/behavior]
role: class
feature: Shared
feature_source: shared
kind: behavior
code_lines: 34
types: 1
methods: 3
fan_in: 8
fan_out: 5
summary_source: documented
candidates: []
source: Poink.Sim/Helpers/SymbolTradingLimits.cs
---
# SymbolTradingLimits.cs

What a symbol allows a trade to be, in the same units the app trades in -- volume in units (cTrader's metadata counts hundredths of a unit), SL/TP distances and prices in price terms.

**Feature:** Shared · **Kind:** class with behaviour · **Code lines:** 34 · **Used by:** 8

## Types
- `SymbolTradingLimits` — record, class with behaviour · 3 methods, 8 properties, 1 field

## Uses
- [[Components/Pages/Trade/Helpers/PositionPnlHelper.cs.md|PositionPnlHelper.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Models/SymbolMetadataModels.cs.md|SymbolMetadataModels.cs]]
- [[Services/Trader/Risk/RiskHelper.cs.md|RiskHelper.cs]]
- [[Services/Trader/Trader.cs.md|Trader.cs]]
