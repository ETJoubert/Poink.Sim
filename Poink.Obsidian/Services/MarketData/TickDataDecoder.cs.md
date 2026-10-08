---
tags: [class, feature/marketdata, kind/mixed]
role: class
feature: MarketData
feature_source: folder
kind: mixed
code_lines: 41
types: 2
methods: 2
fan_in: 1
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Services/MarketData/TickDataDecoder.cs
---
# TickDataDecoder.cs

Decodes cTrader ProtoOAGetTickData responses (newest-first, delta-encoded: the first element is absolute, each subsequent element is the signed difference from the prior one) and merges a bid and an ask series into mid-quote ticks.

**Feature:** MarketData · **Kind:** mixed types · **Code lines:** 41 · **Used by:** 1

## Types
- `PricePoint` — record struct, data type (at most one helper method) · 2 properties
- `TickDataDecoder` — class, static helper · 2 methods, 1 field

## Uses
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
