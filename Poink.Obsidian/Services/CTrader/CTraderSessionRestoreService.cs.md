---
tags: [service]
source: Poink.Sim/Services/CTrader/CTraderSessionRestoreService.cs
---
# CTraderSessionRestoreService.cs

Restores any persisted cTrader session in the background at startup, so a prior login reconnects without blocking application boot or requiring a fresh OAuth redirect.

**Used by:** 1 file (see Backlinks)

## Uses
- [[Services/CTrader/ICTraderSessionService.cs.md|ICTraderSessionService.cs]]
