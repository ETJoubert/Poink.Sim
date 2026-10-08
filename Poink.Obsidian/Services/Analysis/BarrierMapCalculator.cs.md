---
tags: [class]
source: Poink.Sim/Services/Analysis/BarrierMapCalculator.cs
---
# BarrierMapCalculator.cs

Computes a barrier map over ticks held in memory. Each entry is scanned once per side, noting the first moment every stop and every target size is reached and the open result at every time limit; every stop/target/limit cell is then decided from those, so the grid size barely affects the cost.

**Used by:** 2 files (see Backlinks)

## Uses
- [[Models/BarrierMapModels.cs.md|BarrierMapModels.cs]]
