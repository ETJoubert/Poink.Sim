---
tags: [interface, feature/marketdata, kind/interface]
role: interface
feature: MarketData
feature_source: folder
kind: interface
code_lines: 28
types: 1
methods: 15
fan_in: 8
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Services/MarketData/ITickIngest.cs
---
# ITickIngest.cs

Write side of the tick store, kept separate from the read-only ITickRepository. The backfill worker is the single writer; it creates tables on explicit opt-in, appends ticks in bulk, and keeps the tick_table_stats registry current so coverage checks stay cheap.

**Feature:** MarketData · **Kind:** interface · **Code lines:** 28 · **Used by:** 8

## Types
- `ITickIngest` — interface, interface · 15 methods

## Uses
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/MarketData/MarketDataModels.cs.md|MarketDataModels.cs]]
