---
tags: [class]
source: Poink.Sim/Services/Simulation/WatchedSymbolFeed.cs
---
# WatchedSymbolFeed.cs

Replays one watch-only symbol in time lockstep with the traded symbol: each AdvanceTo consumes every tick up to the traded symbol's current timestamp, building that symbol's own bars at the run's aggregation.

**Used by:** 1 file (see Backlinks)

## Uses
- [[Data/Entities/Symbol.cs.md|Symbol.cs]]
- [[Models/SimulationWatchModels.cs.md|SimulationWatchModels.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
- [[Services/Simulation/RingBuffer.cs.md|RingBuffer.cs]]
- [[Services/Simulation/SimulationMarketFeed.cs.md|SimulationMarketFeed.cs]]
