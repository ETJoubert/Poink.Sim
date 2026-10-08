---
tags: [service, feature/dataseriessync, kind/behavior]
role: service
feature: DataSeriesSync
feature_source: folder
kind: behavior
code_lines: 55
types: 1
methods: 4
fan_in: 1
fan_out: 5
summary_source: documented
candidates: []
source: Poink.Sim/Services/DataSeriesSync/DataSeriesSyncService.cs
---
# DataSeriesSyncService.cs

Reads coverage off ITickIngest's registry -- write/backfill paths live elsewhere.

**Feature:** DataSeriesSync · **Kind:** class with behaviour · **Code lines:** 55 · **Used by:** 1

## Types
- `DataSeriesSyncService` — class, class with behaviour · 4 methods, 2 fields, 1 constructor

## Uses
- [[Services/DataSeriesSync/DataSeriesSyncModels.cs.md|DataSeriesSyncModels.cs]]
- [[Services/DataSeriesSync/IDataSeriesSyncService.cs.md|IDataSeriesSyncService.cs]]
- [[Services/MarketData/ITickIngest.cs.md|ITickIngest.cs]]
- [[Services/MarketData/MarketDataModels.cs.md|MarketDataModels.cs]]
- [[Services/MarketData/TickTableNaming.cs.md|TickTableNaming.cs]]
