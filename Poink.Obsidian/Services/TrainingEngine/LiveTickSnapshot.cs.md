---
tags: [class]
source: Poink.Sim/Services/TrainingEngine/LiveTickSnapshot.cs
---
# LiveTickSnapshot.cs

The market and account state after one tick, handed to RunEpisode's optional onTick callback for the Live tab. ClosedTrades is only a copy when a trade closed on this tick, otherwise null.

**Used by:** 3 files (see Backlinks)

## Uses
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
