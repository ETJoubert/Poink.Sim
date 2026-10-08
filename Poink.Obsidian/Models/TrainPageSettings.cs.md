---
tags: [settings]
source: Poink.Sim/Models/TrainPageSettings.cs
---
# TrainPageSettings.cs

Snapshot of the Train page's Run Setup config (Data Range/Brain/Strategy/Episode Timing/Trader), persisted to browser localStorage so it survives navigating away and back -- IRlTrainingService keeps a run going across navigation, but the page's own fields are reset to their defaults every time Blazor recreates the…

**Used by:** 1 file (see Backlinks)

## Uses
- [[Models/SimulationSnapshot.cs.md|SimulationSnapshot.cs]]
