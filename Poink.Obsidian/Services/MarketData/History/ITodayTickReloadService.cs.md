---
tags: [interface, feature/marketdata, kind/mixed]
role: interface
feature: MarketData
feature_source: folder
kind: mixed
code_lines: 8
types: 2
methods: 2
fan_in: 3
fan_out: 0
summary_source: documented
candidates: []
source: Poink.Sim/Services/MarketData/History/ITodayTickReloadService.cs
---
# ITodayTickReloadService.cs

Overrides today's stored ticks for a symbol with a fresh cTrader fetch, e.g. after recorded ticks drifted or a switch left the wrong ticks behind. Writes the connected environment's table, the same one today's ticks live in.

**Feature:** MarketData · **Kind:** mixed types · **Code lines:** 8 · **Used by:** 3

## Types
- `TodayTickReloadResult` — record, data type (at most one helper method) · 5 properties
- `ITodayTickReloadService` — interface, interface · 2 methods

## Uses
- _nothing in Poink.Sim_
