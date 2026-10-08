---
tags: [class]
source: Poink.Sim/Services/MarketData/Recording/LiveTickRecorder.cs
---
# LiveTickRecorder.cs

A table's live ticks are held until today's API catch-up has filled the database up to the first of them, so stored ticks for today stay contiguous and the next history load only needs to fetch past the latest one.

**Used by:** 1 file (see Backlinks)

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
