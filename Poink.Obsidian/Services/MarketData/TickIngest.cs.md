---
tags: [class, feature/marketdata, kind/behavior]
role: class
feature: MarketData
feature_source: folder
kind: behavior
code_lines: 331
types: 5
methods: 27
fan_in: 1
fan_out: 3
summary_source: interface-doc
candidates: []
source: Poink.Sim/Services/MarketData/TickIngest.cs
---
# TickIngest.cs

Write side of the tick store, kept separate from the read-only ITickRepository. The backfill worker is the single writer; it creates tables on explicit opt-in, appends ticks in bulk, and keeps the tick_table_stats registry current so coverage checks stay cheap.

**Feature:** MarketData · **Kind:** class with behaviour · **Code lines:** 331 · **Used by:** 1

## Types
- `TickIngest` — class, class with behaviour · 19 methods, 7 fields, 1 constructor
- `RegistryRow` — record, nested, data type (at most one helper method) · 3 properties
- `StatsRow` — record, nested, data type (at most one helper method) · 3 properties
- `DailyStatRow` — record, nested, data type (at most one helper method) · 6 properties
- `Sql` — class, nested, static helper · 8 methods, 4 properties, 14 fields

## Uses
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/MarketData/ITickIngest.cs.md|ITickIngest.cs]]
- [[Services/MarketData/MarketDataModels.cs.md|MarketDataModels.cs]]
