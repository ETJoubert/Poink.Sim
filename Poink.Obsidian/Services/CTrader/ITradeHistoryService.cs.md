---
tags: [interface]
source: Poink.Sim/Services/CTrader/ITradeHistoryService.cs
---
# ITradeHistoryService.cs

DB-first closed-deal history for the Trade page: reads persisted CTraderDealInfo rows and only calls back to cTrader through ITradeHistoryAccess when a position has closed since the last sync, via TradeDealHistoryService.

**Used by:** 3 files (see Backlinks)

## Uses
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
