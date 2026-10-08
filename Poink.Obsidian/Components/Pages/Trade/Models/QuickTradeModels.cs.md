---
tags: [model, feature/trade, kind/data-bundle]
role: model
feature: Trade
feature_source: folder
kind: data-bundle
code_lines: 16
types: 4
methods: 0
fan_in: 4
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Trade/Models/QuickTradeModels.cs
---
# QuickTradeModels.cs

One side of the quick trade console: how much is open on the charted symbol, whether a request is in flight, and whether its positions are armed to close at breakeven, and when its oldest open position opened.

**Feature:** Trade · **Kind:** bundle of data types · **Code lines:** 16 · **Used by:** 4

## Types
- `QuickTradeSideState` — record, data type (at most one helper method) · 7 properties
- `QuickTradeAwaitedReport` — record, data type (at most one helper method) · 4 properties
- `QuickTradeOpenRequest` — record, data type (at most one helper method) · 2 properties
- `TradeConsoleMode` — enum, enum · 2 values

## Uses
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
