---
tags: [class]
source: Poink.Sim/Services/TrainingEngine/TickWindow.cs
---
# TickWindow.cs

A no-copy view over a slice of a tick list. The episode loop grows the view's end one tick at a time so features only ever see ticks up to the current one, and feature math narrows the start to the trailing window. Views over views are flattened onto the underlying list.

**Used by:** 3 files (see Backlinks)

## Uses
- [[Repository/Interfaces/ITickRepository.cs.md|ITickRepository.cs]]
