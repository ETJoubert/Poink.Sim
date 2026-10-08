---
tags: [model]
source: Poink.Sim/Services/TrainingEngine/TrainingRunModels.cs
---
# TrainingRunModels.cs

The page watching a TrainingRunService run. EpisodeStarting fires before every episode; IsLive is read once per episode, and when false (or no observer is attached) the episode runs headless at full speed with no live callbacks.

**Used by:** 2 files (see Backlinks)

## Uses
- [[Services/TrainingEngine/LiveEpisodeSnapshot.cs.md|LiveEpisodeSnapshot.cs]]
- [[Services/TrainingEngine/LiveTickSnapshot.cs.md|LiveTickSnapshot.cs]]
