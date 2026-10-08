---
tags: [interface]
source: Poink.Sim/Services/MarketData/ITickIngest.cs
---
# ITickIngest.cs

Write side of the tick store, kept separate from the read-only ITickRepository. The backfill worker is the single writer; it creates tables on explicit opt-in, appends ticks in bulk, and keeps the tick_table_stats registry current so coverage checks stay cheap.

**Used by:** 8 files (see Backlinks)

## Uses
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/MarketData/MarketDataModels.cs.md|MarketDataModels.cs]]
