---
tags: [interface]
source: Poink.Sim/Services/CTrader/IAccountAccess.cs
---
# IAccountAccess.cs

The session slice the account/connection chrome needs: connection state, account list, and login/logout. Implemented by the session, narrowed off ICTraderSessionService so callers can't reach watchlist or trade-execution members they have no business touching.

**Used by:** 10 files (see Backlinks)

## Uses
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
