---
tags: [model, feature/marketdata, kind/data-bundle]
role: model
feature: MarketData
feature_source: folder
kind: data-bundle
code_lines: 63
types: 8
methods: 0
fan_in: 15
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Services/MarketData/MarketDataModels.cs
---
# MarketDataModels.cs

A single merged bid/ask quote for a symbol, carrying cTrader's server timestamp when available.

**Feature:** MarketData · **Kind:** bundle of data types · **Code lines:** 63 · **Used by:** 15

## Types
- `MarketTick` — record struct, data type (at most one helper method) · 5 properties
- `TickCoverage` — record struct, data type (at most one helper method) · 4 properties
- `TickIngestState` — enum, enum · 6 values
- `TickTableStatRow` — record, data type (at most one helper method) · 8 properties
- `BackfillTrigger` — enum, enum · 2 values
- `BackfillLogEntry` — record, data type (at most one helper method) · 9 properties
- `TickDailyStatRow` — record, data type (at most one helper method) · 6 properties
- `SymbolIngestStatus` — record, data type (at most one helper method) · 12 properties

## Uses
- [[Data/Entities/Symbol.cs.md|Symbol.cs]]
