---
tags: [repository, feature/shared, kind/behavior]
role: repository
feature: Shared
feature_source: shared
kind: behavior
code_lines: 122
types: 4
methods: 12
fan_in: 14
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Repository/TickRepository.cs
---
# TickRepository.cs

Reads bid/ask ticks and per-table statistics from the Postgres tick tables, caching the table list across calls.

**Feature:** Shared · **Kind:** class with behaviour · **Code lines:** 122 · **Used by:** 14

## Types
- `TickRepository` — class, class with behaviour · 7 methods, 2 properties, 5 fields, 1 constructor
- `TickTableStats` — record, nested, data type (at most one helper method) · 3 properties
- `TickTableStatsRow` — record, nested, data type (at most one helper method) · 4 properties
- `Sql` — class, nested, static helper · 5 methods, 5 fields

## Uses
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
