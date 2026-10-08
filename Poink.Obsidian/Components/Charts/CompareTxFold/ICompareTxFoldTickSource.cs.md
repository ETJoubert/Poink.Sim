---
tags: [interface]
source: Poink.Sim/Components/Charts/CompareTxFold/ICompareTxFoldTickSource.cs
---
# ICompareTxFoldTickSource.cs

Lets a host supply CompareTxFold's ticks instead of the tick tables, e.g. stored history joined to live ticks not yet written, so the snapshot can follow every tick. Called off the render thread, once per symbol.

**Used by:** 4 files (see Backlinks)

## Uses
- [[Components/Charts/CompareTxFold/CompareTxFoldModels.cs.md|CompareTxFoldModels.cs]]
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
