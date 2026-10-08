---
tags: [class, feature/marketdata, kind/mixed]
role: class
feature: MarketData
feature_source: folder
kind: mixed
code_lines: 56
types: 4
methods: 6
fan_in: 1
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Services/MarketData/Recording/LiveTickBuffer.cs
---
# LiveTickBuffer.cs

Thread-safe per-table accumulator between the tick stream (writer) and the recorder's flush loop (reader).

**Feature:** MarketData · **Kind:** mixed types · **Code lines:** 56 · **Used by:** 1

## Types
- `LiveTickBatch` — record, data type (at most one helper method) · 4 properties
- `PendingLiveTable` — record struct, data type (at most one helper method) · 4 properties
- `LiveTickBuffer` — class, class with behaviour · 6 methods, 1 property, 2 fields
- `PendingTicks` — class, nested, data type (at most one helper method) · 3 properties

## Uses
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
