---
tags: [service]
source: Poink.Sim/Services/MarketData/History/TickHistoryService.cs
---
# TickHistoryService.cs

History before today always reads the live table, since the Data page import only writes live tables; today's ticks read and write the connected environment's table, so a demo account keeps its own diverging prices.

**Used by:** 1 file (see Backlinks)

## Uses
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/CTrader/ICTraderHistory.cs.md|ICTraderHistory.cs]]
- [[Services/MarketData/History/ITickHistoryService.cs.md|ITickHistoryService.cs]]
- [[Services/MarketData/History/TickHistoryModels.cs.md|TickHistoryModels.cs]]
- [[Services/MarketData/History/TickHistoryPlanner.cs.md|TickHistoryPlanner.cs]]
- [[Services/MarketData/ITickIngest.cs.md|ITickIngest.cs]]
- [[Services/MarketData/MarketDataModels.cs.md|MarketDataModels.cs]]
- [[Services/MarketData/TickTableNaming.cs.md|TickTableNaming.cs]]
