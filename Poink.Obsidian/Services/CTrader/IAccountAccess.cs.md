---
tags: [interface, feature/ctrader, kind/interface]
role: interface
feature: CTrader
feature_source: folder
kind: interface
code_lines: 15
types: 1
methods: 3
fan_in: 9
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Services/CTrader/IAccountAccess.cs
---
# IAccountAccess.cs

The session slice the account/connection chrome needs: connection state, account list, and login/logout. Implemented by the session, narrowed off ICTraderSessionService so callers can't reach watchlist or trade-execution members they have no business touching.

**Feature:** CTrader · **Kind:** interface · **Code lines:** 15 · **Used by:** 9

## Types
- `IAccountAccess` — interface, interface · 3 methods, 5 properties, 1 event

## Uses
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
