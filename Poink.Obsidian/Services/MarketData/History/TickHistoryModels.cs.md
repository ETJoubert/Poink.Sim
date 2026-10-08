---
tags: [model, feature/marketdata, kind/data-bundle]
role: model
feature: MarketData
feature_source: folder
kind: data-bundle
code_lines: 30
types: 3
methods: 0
fan_in: 6
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Services/MarketData/History/TickHistoryModels.cs
---
# TickHistoryModels.cs

A tick-history window for one symbol; Trigger is recorded against any ticks the API fetch commits.

**Feature:** MarketData · **Kind:** bundle of data types · **Code lines:** 30 · **Used by:** 6

## Types
- `TickHistoryRequest` — record, data type (at most one helper method) · 5 properties
- `TickHistoryResult` — record, data type (at most one helper method) · 7 properties
- `TickHistoryPlan` — record struct, data type (at most one helper method) · 8 properties

## Uses
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/MarketData/MarketDataModels.cs.md|MarketDataModels.cs]]
