---
tags: [service]
source: Poink.Sim/Components/Pages/Trade/Services/PositionCloseService.cs
---
# PositionCloseService.cs

The quick console's close helpers. Armed sides close each position on its own once price is back to its entry or better; a side disarms itself once it has no positions left or a close fails, and every side disarms when the selected account changes.

**Used by:** 1 file (see Backlinks)

## Uses
- [[Components/Pages/Trade/Helpers/PositionCloseHelper.cs.md|PositionCloseHelper.cs]]
- [[Components/Pages/Trade/Models/PositionCloseModels.cs.md|PositionCloseModels.cs]]
- [[Components/Pages/Trade/Services/IPositionCloseService.cs.md|IPositionCloseService.cs]]
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
- [[Services/CTrader/IAccountAccess.cs.md|IAccountAccess.cs]]
- [[Services/CTrader/ITickStreamAccess.cs.md|ITickStreamAccess.cs]]
- [[Services/CTrader/ITradeExecutionAccess.cs.md|ITradeExecutionAccess.cs]]
- [[Services/MarketData/MarketDataModels.cs.md|MarketDataModels.cs]]
