---
tags: [class]
source: Poink.Sim/Services/TrainingEngine/LiveBarBuilder.cs
---
# LiveBarBuilder.cs

Incrementally folds a tick stream into fixed-width OHLC bars, mid-price (Bid+Ask)/2 per tick -- feeds the Live tab's chart from an episode's ticks as they're processed, one at a time, without re-aggregating history on every call.

**Used by:** 1 file (see Backlinks)

## Uses
- [[Models/TraderModels.cs.md|TraderModels.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
