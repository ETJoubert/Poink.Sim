---
tags: [interface, feature/ctrader, kind/interface]
role: interface
feature: CTrader
feature_source: folder
kind: interface
code_lines: 16
types: 1
methods: 1
fan_in: 6
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Services/CTrader/ICTraderHistory.cs
---
# ICTraderHistory.cs

Internal history fetch over the authorized cTrader connection, kept off the public ICTraderSessionService because it returns the internal Tick type. Implemented by the session, which owns the connection and the selected account.

**Feature:** CTrader · **Kind:** interface · **Code lines:** 16 · **Used by:** 6

## Types
- `ICTraderHistory` — interface, interface · 1 method, 3 properties

## Uses
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
