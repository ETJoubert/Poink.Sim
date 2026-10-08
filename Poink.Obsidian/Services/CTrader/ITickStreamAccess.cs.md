---
tags: [interface]
source: Poink.Sim/Services/CTrader/ITickStreamAccess.cs
---
# ITickStreamAccess.cs

The session slice TradeChartPanel needs: live quotes/positions and the per-tick feed for aggregating bars. Implemented by the session, narrowed off ICTraderSessionService so the chart panel can't reach order placement or account/lifecycle members it has no business touching.

**Used by:** 9 files (see Backlinks)

## Uses
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
- [[Services/MarketData/MarketDataModels.cs.md|MarketDataModels.cs]]
