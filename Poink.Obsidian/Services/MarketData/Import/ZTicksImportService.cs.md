---
tags: [service, feature/marketdata, kind/behavior]
role: service
feature: MarketData
feature_source: folder
kind: behavior
code_lines: 140
types: 1
methods: 6
fan_in: 1
fan_out: 6
summary_source: documented
candidates: []
source: Poink.Sim/Services/MarketData/Import/ZTicksImportService.cs
---
# ZTicksImportService.cs

Writes through the same ITickIngest the live backfill worker uses, so imported history is indistinguishable from streamed ticks.

**Feature:** MarketData · **Kind:** class with behaviour · **Code lines:** 140 · **Used by:** 1

## Types
- `ZTicksImportService` — class, class with behaviour · 6 methods, 5 properties, 6 fields, 1 constructor, 1 event

## Uses
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/MarketData/Import/IZTicksImportService.cs.md|IZTicksImportService.cs]]
- [[Services/MarketData/Import/ZTicksImportModels.cs.md|ZTicksImportModels.cs]]
- [[Services/MarketData/ITickIngest.cs.md|ITickIngest.cs]]
- [[Services/MarketData/MarketDataModels.cs.md|MarketDataModels.cs]]
- [[Services/MarketData/TickTableNaming.cs.md|TickTableNaming.cs]]
