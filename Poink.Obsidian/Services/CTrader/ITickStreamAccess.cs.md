---
tags: [interface, feature/ctrader, kind/interface]
role: interface
feature: CTrader
feature_source: folder
kind: interface
code_lines: 11
types: 1
methods: 0
fan_in: 8
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Services/CTrader/ITickStreamAccess.cs
---
# ITickStreamAccess.cs

The session slice TradeChartPanel needs: live quotes/positions and the per-tick feed for aggregating bars. Implemented by the session, narrowed off ICTraderSessionService so the chart panel can't reach order placement or account/lifecycle members it has no business touching.

**Feature:** CTrader · **Kind:** interface · **Code lines:** 11 · **Used by:** 8

## Types
- `ITickStreamAccess` — interface, interface · 5 properties, 1 event

## Uses
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
- [[Services/MarketData/MarketDataModels.cs.md|MarketDataModels.cs]]
