---
tags: [interface]
source: Poink.Sim/Services/CTrader/IWatchlistAccess.cs
---
# IWatchlistAccess.cs

The session slice SymbolWatchlistPanel needs: watch/unwatch a symbol and read the live watchlist. Implemented by the session, narrowed off ICTraderSessionService so the watchlist panel can't reach order placement or account/lifecycle members it has no business touching.

**Used by:** 7 files (see Backlinks)

## Uses
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
