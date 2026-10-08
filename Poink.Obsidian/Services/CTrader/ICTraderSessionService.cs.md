---
tags: [interface]
source: Poink.Sim/Services/CTrader/ICTraderSessionService.cs
---
# ICTraderSessionService.cs

Single-user cTrader Open API session for this dashboard: owns the OAuth token and the one live connection, exposing connection state, the authorized account's symbols, and a live-streaming watchlist to the UI. The token is held in memory only, so a restart requires logging in again (no refresh-token persistence yet).

**Used by:** 7 files (see Backlinks)

## Uses
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
- [[Services/MarketData/MarketDataModels.cs.md|MarketDataModels.cs]]
