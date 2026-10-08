---
tags: [class]
source: Poink.Sim/Services/Simulation/PrefetchingTickReader.cs
---
# PrefetchingTickReader.cs

Reads ticks from a source enumerator on a background task in fixed-size blocks, so fetching the next ticks overlaps with replaying the current ones. Yields exactly the source's ticks, in order.

**Used by:** 2 files (see Backlinks)

## Uses
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
