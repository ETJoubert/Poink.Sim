---
tags: [interface, feature/marketdata, kind/interface]
role: interface
feature: MarketData
feature_source: folder
kind: interface
code_lines: 14
types: 1
methods: 1
fan_in: 2
fan_out: 1
summary_source: documented
candidates: []
source: Poink.Sim/Services/MarketData/IHistoricalTickSource.cs
---
# IHistoricalTickSource.cs

Fetches historical ticks for the pre-stream gap the backfill worker cannot fill from the live stream. Backed by cTrader's ProtoOAGetTickData (bid + ask series merged into mid quotes) once implemented; the current stub returns nothing, so only empty or already-current tables persist.

**Feature:** MarketData · **Kind:** interface · **Code lines:** 14 · **Used by:** 2

## Types
- `IHistoricalTickSource` — interface, interface · 1 method, 1 property

## Uses
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
