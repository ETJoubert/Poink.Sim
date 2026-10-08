---
tags: [interface]
source: Poink.Sim/Services/CTrader/ITradeHistoryAccess.cs
---
# ITradeHistoryAccess.cs

The session slice closed-trade history callers need. Narrowed off ICTraderSessionService so callers can't reach watchlist or account/lifecycle members they have no business touching.

**Used by:** 3 files (see Backlinks)

## Uses
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
