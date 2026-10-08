---
tags: [class]
source: Poink.Sim/Services/MarketData/History/TickHistoryPlanner.cs
---
# TickHistoryPlanner.cs

Splits a tick-history window into database and API ranges so only today's unstored ticks cost an API call.

**Used by:** 4 files (see Backlinks)

## Uses
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/MarketData/History/TickHistoryModels.cs.md|TickHistoryModels.cs]]
