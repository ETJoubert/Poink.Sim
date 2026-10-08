---
tags: [model]
source: Poink.Sim/Models/TrainingResultModels.cs
---
# TrainingResultModels.cs

Snapshot of a training run's Run Setup config (Data Range/Brain/Strategy/Episode Timing/Trader), taken at save time -- decoupled from TrainPageSettings, which persists the page's live UI state rather than a saved result.

**Used by:** 4 files (see Backlinks)

## Uses
- [[Models/SimulationSnapshot.cs.md|SimulationSnapshot.cs]]
- [[Services/Training/RlEpisode.cs.md|RlEpisode.cs]]
