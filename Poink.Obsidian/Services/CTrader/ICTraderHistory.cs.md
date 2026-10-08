---
tags: [interface]
source: Poink.Sim/Services/CTrader/ICTraderHistory.cs
---
# ICTraderHistory.cs

Internal history fetch over the authorized cTrader connection, kept off the public ICTraderSessionService because it returns the internal Tick type. Implemented by the session, which owns the connection and the selected account.

**Used by:** 7 files (see Backlinks)

## Uses
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
