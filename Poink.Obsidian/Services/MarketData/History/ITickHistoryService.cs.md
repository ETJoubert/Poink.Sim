---
tags: [interface]
source: Poink.Sim/Services/MarketData/History/ITickHistoryService.cs
---
# ITickHistoryService.cs

Database-first tick history: days before today come from the tick tables (filled by the Data page import), and only today's not-yet-stored ticks are fetched from the API -- and committed, so the next read is free.

**Used by:** 4 files (see Backlinks)

## Uses
- [[Services/MarketData/History/TickHistoryModels.cs.md|TickHistoryModels.cs]]
- [[Services/MarketData/MarketDataModels.cs.md|MarketDataModels.cs]]
