---
tags: [service]
source: Poink.Sim/Services/CTrader/CTraderSessionService.cs
---
# CTraderSessionService.cs

Single-user cTrader Open API session for this dashboard: owns the OAuth token and the one live connection, exposing connection state, the authorized account's symbols, and a live-streaming watchlist to the UI. The token is held in memory only, so a restart requires logging in again (no refresh-token persistence yet).

**Used by:** 1 file (see Backlinks)

## Uses
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
- [[Services/CTrader/IAccountAccess.cs.md|IAccountAccess.cs]]
- [[Services/CTrader/ICTraderHistory.cs.md|ICTraderHistory.cs]]
- [[Services/CTrader/ICTraderSessionService.cs.md|ICTraderSessionService.cs]]
- [[Services/CTrader/ITickStreamAccess.cs.md|ITickStreamAccess.cs]]
- [[Services/CTrader/ITradeExecutionAccess.cs.md|ITradeExecutionAccess.cs]]
- [[Services/CTrader/ITradeHistoryAccess.cs.md|ITradeHistoryAccess.cs]]
- [[Services/CTrader/IWatchlistAccess.cs.md|IWatchlistAccess.cs]]
- [[Services/MarketData/MarketDataModels.cs.md|MarketDataModels.cs]]
- [[Services/MarketData/TickDataDecoder.cs.md|TickDataDecoder.cs]]
