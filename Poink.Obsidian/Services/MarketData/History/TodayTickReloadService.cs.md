---
tags: [service]
source: Poink.Sim/Services/MarketData/History/TodayTickReloadService.cs
---
# TodayTickReloadService.cs

Fetches before deleting, so a failed or cross-account fetch leaves today's stored ticks untouched; the delete and insert then commit together. Live ticks still buffered from before the fetch's end are dropped, since writing them afterwards would mix recorded quotes back into the freshly fetched day.

**Used by:** 1 file (see Backlinks)

## Uses
- [[Services/CTrader/ICTraderHistory.cs.md|ICTraderHistory.cs]]
- [[Services/MarketData/History/ITodayTickReloadService.cs.md|ITodayTickReloadService.cs]]
- [[Services/MarketData/History/TickHistoryPlanner.cs.md|TickHistoryPlanner.cs]]
- [[Services/MarketData/ITickIngest.cs.md|ITickIngest.cs]]
- [[Services/MarketData/MarketDataModels.cs.md|MarketDataModels.cs]]
- [[Services/MarketData/Recording/ILiveTickRecorder.cs.md|ILiveTickRecorder.cs]]
- [[Services/MarketData/TickTableNaming.cs.md|TickTableNaming.cs]]
