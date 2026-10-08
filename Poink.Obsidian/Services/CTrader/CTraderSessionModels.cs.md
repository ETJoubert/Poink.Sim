---
tags: [model, feature/ctrader, kind/data-bundle]
role: model
feature: CTrader
feature_source: folder
kind: data-bundle
code_lines: 56
types: 9
methods: 0
fan_in: 35
fan_out: 0
summary_source: documented
candidates: []
source: Poink.Sim/Services/CTrader/CTraderSessionModels.cs
---
# CTraderSessionModels.cs

An open position, kept in sync via an initial reconcile plus live execution-event push.

**Feature:** CTrader · **Kind:** bundle of data types · **Code lines:** 56 · **Used by:** 35

## Types
- `CTraderConnectionState` — enum, enum · 4 values
- `CTraderAccountInfo` — record, data type (at most one helper method) · 3 properties
- `CTraderPositionInfo` — record, data type (at most one helper method) · 10 properties
- `CTraderSymbolInfo` — record, data type (at most one helper method) · 8 properties
- `CTraderSymbolQuote` — record, data type (at most one helper method) · 5 properties
- `CTraderBar` — record, data type (at most one helper method) · 6 properties
- `CTraderOrderType` — enum, enum · 3 values
- `CTraderOrderResult` — record, data type (at most one helper method) · 2 properties
- `CTraderDealInfo` — record, data type (at most one helper method) · 11 properties

## Uses
- _nothing in Poink.Sim_
