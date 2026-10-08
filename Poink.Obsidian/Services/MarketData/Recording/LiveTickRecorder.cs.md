---
tags: [class, feature/marketdata, kind/behavior]
role: class
feature: MarketData
feature_source: folder
kind: behavior
code_lines: 146
types: 4
methods: 12
fan_in: 1
fan_out: 10
summary_source: documented
candidates: []
source: Poink.Sim/Services/MarketData/Recording/LiveTickRecorder.cs
---
# LiveTickRecorder.cs

A table's live ticks are held until today's API catch-up has filled the database up to the first of them, so stored ticks for today stay contiguous and the next history load only needs to fetch past the latest one.

**Feature:** MarketData · **Kind:** class with behaviour · **Code lines:** 146 · **Used by:** 1

## Types
- `LiveTickRecorder` — class, class with behaviour · 11 methods, 1 property, 18 fields, 1 constructor
- `CatchUpStatus` — enum, nested, enum · 4 values
- `CatchUpState` — record struct, nested, data type (at most one helper method) · 2 properties
- `Lease` — class, nested, single-method class · 1 method, 1 field

## Uses
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/CTrader/ICTraderHistory.cs.md|ICTraderHistory.cs]]
- [[Services/CTrader/ITickStreamAccess.cs.md|ITickStreamAccess.cs]]
- [[Services/MarketData/History/ITickHistoryService.cs.md|ITickHistoryService.cs]]
- [[Services/MarketData/History/TickHistoryPlanner.cs.md|TickHistoryPlanner.cs]]
- [[Services/MarketData/ITickIngest.cs.md|ITickIngest.cs]]
- [[Services/MarketData/MarketDataModels.cs.md|MarketDataModels.cs]]
- [[Services/MarketData/Recording/ILiveTickRecorder.cs.md|ILiveTickRecorder.cs]]
- [[Services/MarketData/Recording/LiveTickBuffer.cs.md|LiveTickBuffer.cs]]
- [[Services/MarketData/TickTableNaming.cs.md|TickTableNaming.cs]]
