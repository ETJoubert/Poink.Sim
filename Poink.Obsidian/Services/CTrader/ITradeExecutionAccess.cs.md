---
tags: [interface, feature/ctrader, kind/interface]
role: interface
feature: CTrader
feature_source: folder
kind: interface
code_lines: 17
types: 1
methods: 3
fan_in: 5
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Services/CTrader/ITradeExecutionAccess.cs
---
# ITradeExecutionAccess.cs

The session slice trade-execution callers need: place/close orders and close positions. Implemented by the session, narrowed off ICTraderSessionService so callers can't reach watchlist or account/lifecycle members they have no business touching.

**Feature:** CTrader · **Kind:** interface · **Code lines:** 17 · **Used by:** 5

## Types
- `ITradeExecutionAccess` — interface, interface · 3 methods, 1 property

## Uses
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
