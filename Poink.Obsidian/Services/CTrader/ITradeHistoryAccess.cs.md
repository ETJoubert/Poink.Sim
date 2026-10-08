---
tags: [interface, feature/ctrader, kind/interface]
role: interface
feature: CTrader
feature_source: folder
kind: interface
code_lines: 13
types: 1
methods: 1
fan_in: 3
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Services/CTrader/ITradeHistoryAccess.cs
---
# ITradeHistoryAccess.cs

The session slice closed-trade history callers need. Narrowed off ICTraderSessionService so callers can't reach watchlist or account/lifecycle members they have no business touching.

**Feature:** CTrader · **Kind:** interface · **Code lines:** 13 · **Used by:** 3

## Types
- `ITradeHistoryAccess` — interface, interface · 1 method, 1 property, 1 event

## Uses
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
