---
tags: [service, feature/marketdata, kind/behavior]
role: service
feature: MarketData
feature_source: folder
kind: behavior
code_lines: 63
types: 1
methods: 2
fan_in: 1
fan_out: 7
summary_source: documented
candidates: []
source: Poink.Sim/Services/MarketData/History/TodayTickReloadService.cs
---
# TodayTickReloadService.cs

Fetches before deleting, so a failed or cross-account fetch leaves today's stored ticks untouched; the delete and insert then commit together. Live ticks still buffered from before the fetch's end are dropped, since writing them afterwards would mix recorded quotes back into the freshly fetched day.

**Feature:** MarketData · **Kind:** class with behaviour · **Code lines:** 63 · **Used by:** 1

## Types
- `TodayTickReloadService` — class, class with behaviour · 2 methods, 4 fields, 1 constructor

## Uses
- [[Services/CTrader/ICTraderHistory.cs.md|ICTraderHistory.cs]]
- [[Services/MarketData/History/ITodayTickReloadService.cs.md|ITodayTickReloadService.cs]]
- [[Services/MarketData/History/TickHistoryPlanner.cs.md|TickHistoryPlanner.cs]]
- [[Services/MarketData/ITickIngest.cs.md|ITickIngest.cs]]
- [[Services/MarketData/MarketDataModels.cs.md|MarketDataModels.cs]]
- [[Services/MarketData/Recording/ILiveTickRecorder.cs.md|ILiveTickRecorder.cs]]
- [[Services/MarketData/TickTableNaming.cs.md|TickTableNaming.cs]]
