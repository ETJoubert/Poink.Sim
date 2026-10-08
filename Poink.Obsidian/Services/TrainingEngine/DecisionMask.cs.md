---
tags: [class]
source: Poink.Sim/Services/TrainingEngine/DecisionMask.cs
---
# DecisionMask.cs

Gates whether the network is even asked to decide -- once per closed bar, once per configured interval, or on every tick. Action-legality masking lives in the Output tab, not here.

**Used by:** 1 file (see Backlinks)

## Uses
- [[Models/TrainingModels/TrainingTabOptionsModel.cs.md|TrainingTabOptionsModel.cs]]
- [[Services/Brain/Neural/BrainCatalog.cs.md|BrainCatalog.cs]]
