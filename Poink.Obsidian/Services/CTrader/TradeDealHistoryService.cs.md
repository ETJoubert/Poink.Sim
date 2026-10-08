---
tags: [service, feature/ctrader, kind/behavior]
role: service
feature: CTrader
feature_source: folder
kind: behavior
code_lines: 55
types: 1
methods: 2
fan_in: 1
fan_out: 4
summary_source: documented
candidates: []
source: Poink.Sim/Services/CTrader/TradeDealHistoryService.cs
---
# TradeDealHistoryService.cs

Wraps ITradeHistoryAccess with a DB-first cache: reads always come from ITradeDealRepository, and cTrader is only re-queried when PositionClosed has fired since the last sync (the "history sentinel").

**Feature:** CTrader · **Kind:** class with behaviour · **Code lines:** 55 · **Used by:** 1

## Types
- `TradeDealHistoryService` — class, class with behaviour · 2 methods, 7 fields, 1 constructor

## Uses
- [[Repository/Interfaces/ITradeDealRepository.cs.md|ITradeDealRepository.cs]]
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
- [[Services/CTrader/ITradeHistoryAccess.cs.md|ITradeHistoryAccess.cs]]
- [[Services/CTrader/ITradeHistoryService.cs.md|ITradeHistoryService.cs]]
