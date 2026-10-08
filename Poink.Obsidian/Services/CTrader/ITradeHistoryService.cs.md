---
tags: [interface, feature/ctrader, kind/interface]
role: interface
feature: CTrader
feature_source: folder
kind: interface
code_lines: 11
types: 1
methods: 1
fan_in: 3
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Services/CTrader/ITradeHistoryService.cs
---
# ITradeHistoryService.cs

DB-first closed-deal history for the Trade page: reads persisted CTraderDealInfo rows and only calls back to cTrader through ITradeHistoryAccess when a position has closed since the last sync, via TradeDealHistoryService.

**Feature:** CTrader · **Kind:** interface · **Code lines:** 11 · **Used by:** 3

## Types
- `ITradeHistoryService` — interface, interface · 1 method

## Uses
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
