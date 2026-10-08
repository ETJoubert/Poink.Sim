---
tags: [service]
source: Poink.Sim/Services/CTrader/TradeDealHistoryService.cs
---
# TradeDealHistoryService.cs

Wraps ITradeHistoryAccess with a DB-first cache: reads always come from ITradeDealRepository, and cTrader is only re-queried when PositionClosed has fired since the last sync (the "history sentinel").

**Used by:** 1 file (see Backlinks)

## Uses
- [[Repository/Interfaces/ITradeDealRepository.cs.md|ITradeDealRepository.cs]]
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
- [[Services/CTrader/ITradeHistoryAccess.cs.md|ITradeHistoryAccess.cs]]
- [[Services/CTrader/ITradeHistoryService.cs.md|ITradeHistoryService.cs]]
