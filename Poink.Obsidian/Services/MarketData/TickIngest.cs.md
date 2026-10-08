---
tags: [class]
source: Poink.Sim/Services/MarketData/TickIngest.cs
---
# TickIngest.cs

Write side of the tick store, kept separate from the read-only ITickRepository. The backfill worker is the single writer; it creates tables on explicit opt-in, appends ticks in bulk, and keeps the tick_table_stats registry current so coverage checks stay cheap.

**Used by:** 1 file (see Backlinks)

## Uses
- [[Data/Entities/Symbol.cs.md|Symbol.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/MarketData/ITickIngest.cs.md|ITickIngest.cs]]
- [[Services/MarketData/MarketDataModels.cs.md|MarketDataModels.cs]]
