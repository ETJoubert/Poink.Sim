---
tags: [interface]
source: Poink.Sim/Components/Pages/Trade/Services/IPositionCloseService.cs
---
# IPositionCloseService.cs

The quick console's close helpers. Armed sides close each position on its own once price is back to its entry or better; a side disarms itself once it has no positions left or a close fails, and every side disarms when the selected account changes.

**Used by:** 3 files (see Backlinks)

## Uses
- [[Components/Pages/Trade/Models/PositionCloseModels.cs.md|PositionCloseModels.cs]]
- [[Services/CTrader/CTraderSessionModels.cs.md|CTraderSessionModels.cs]]
