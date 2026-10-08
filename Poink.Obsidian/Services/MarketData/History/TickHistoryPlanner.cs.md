---
tags: [class, feature/marketdata, kind/static-helper]
role: class
feature: MarketData
feature_source: folder
kind: static-helper
code_lines: 28
types: 1
methods: 6
fan_in: 4
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Services/MarketData/History/TickHistoryPlanner.cs
---
# TickHistoryPlanner.cs

Splits a tick-history window into database and API ranges so only today's unstored ticks cost an API call.

**Feature:** MarketData · **Kind:** static helper · **Code lines:** 28 · **Used by:** 4

## Types
- `TickHistoryPlanner` — class, static helper · 6 methods

## Uses
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/MarketData/History/TickHistoryModels.cs.md|TickHistoryModels.cs]]
