---
tags: [class]
source: Poink.Sim/Services/TrainingEngine/LiveEpisodeSnapshot.cs
---
# LiveEpisodeSnapshot.cs

One decision's worth of in-progress episode state, handed to RunEpisode's optional onDecision callback for the Live tab -- ClosedTrades is a defensive copy of the trader's trade log so far, since the caller may read it from a different thread than the one still appending to it.

**Used by:** 3 files (see Backlinks)

## Uses
- [[Models/SimulationAccount.cs.md|SimulationAccount.cs]]
- [[Models/SimulationDecision.cs.md|SimulationDecision.cs]]
- [[Models/TraderModels.cs.md|TraderModels.cs]]
- [[Models/TrainingModels/FeatureModel.cs.md|FeatureModel.cs]]
