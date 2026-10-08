---
tags: [interface]
source: Poink.Sim/Services/CTrader/ITradeExecutionAccess.cs
---
# ITradeExecutionAccess.cs

The session slice trade-execution callers need: place/close orders and close positions. Implemented by the session, narrowed off ICTraderSessionService so callers can't reach watchlist or account/lifecycle members they have no business touching.

**Used by:** 6 files (see Backlinks)

## Uses
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
