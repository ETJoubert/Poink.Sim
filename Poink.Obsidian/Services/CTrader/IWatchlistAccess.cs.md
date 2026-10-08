---
tags: [interface, feature/ctrader, kind/interface]
role: interface
feature: CTrader
feature_source: folder
kind: interface
code_lines: 13
types: 1
methods: 3
fan_in: 6
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Services/CTrader/IWatchlistAccess.cs
---
# IWatchlistAccess.cs

The session slice SymbolWatchlistPanel needs: watch/unwatch a symbol and read the live watchlist. Implemented by the session, narrowed off ICTraderSessionService so the watchlist panel can't reach order placement or account/lifecycle members it has no business touching.

**Feature:** CTrader · **Kind:** interface · **Code lines:** 13 · **Used by:** 6

## Types
- `IWatchlistAccess` — interface, interface · 3 methods, 4 properties, 1 event

## Uses
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
