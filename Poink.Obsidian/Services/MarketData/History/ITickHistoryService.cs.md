---
tags: [interface, feature/marketdata, kind/interface]
role: interface
feature: MarketData
feature_source: folder
kind: interface
code_lines: 13
types: 1
methods: 2
fan_in: 4
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Services/MarketData/History/ITickHistoryService.cs
---
# ITickHistoryService.cs

Database-first tick history: days before today come from the tick tables (filled by the Data page import), and only today's not-yet-stored ticks are fetched from the API -- and committed, so the next read is free.

**Feature:** MarketData · **Kind:** interface · **Code lines:** 13 · **Used by:** 4

## Types
- `ITickHistoryService` — interface, interface · 2 methods

## Uses
- [[Services/MarketData/History/TickHistoryModels.cs.md|TickHistoryModels.cs]]
- [[Services/MarketData/MarketDataModels.cs.md|MarketDataModels.cs]]
