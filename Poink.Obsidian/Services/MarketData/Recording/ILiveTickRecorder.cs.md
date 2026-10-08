---
tags: [interface, feature/marketdata, kind/interface]
role: interface
feature: MarketData
feature_source: folder
kind: interface
code_lines: 6
types: 1
methods: 2
fan_in: 4
fan_out: 0
summary_source: documented
candidates: []
source: Poink.Sim/Services/MarketData/Recording/ILiveTickRecorder.cs
---
# ILiveTickRecorder.cs

Commits the session's streamed ticks to the tick tables while at least one lease is held (the Trade page holds one while open), so today's database coverage grows without repeat API calls.

**Feature:** MarketData · **Kind:** interface · **Code lines:** 6 · **Used by:** 4

## Types
- `ILiveTickRecorder` — interface, interface · 2 methods, 1 property

## Uses
- _nothing in Poink.Sim_
